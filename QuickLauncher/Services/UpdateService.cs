using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace QuickLauncher.Services;

public sealed class UpdateService {
    private const string GitHubApiUrl = "https://api.github.com/repos/wfsh2026/Tool-QuickLauncher/releases/latest";
    private static readonly HttpClient Http = new() {
        DefaultRequestHeaders = {
            { "User-Agent", "QuickLauncher" },
            { "Accept", "application/vnd.github+json" }
        },
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<UpdateInfo?> CheckForUpdateAsync() {
        using var response = await Http.GetAsync(GitHubApiUrl);
        if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.TooManyRequests) {
            throw new InvalidOperationException("GitHub 拒绝了版本查询，可能已触发访问限流。请稍后重启程序重试，或前往发布页面下载。");
        }
        if (!response.IsSuccessStatusCode) {
            throw new InvalidOperationException($"版本查询失败：HTTP {(int)response.StatusCode}。请检查网络和发布仓库是否可访问。");
        }

        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>();
        var remoteVersion = ParseVersion(release?.TagName);
        if (release is null || remoteVersion is null) {
            throw new InvalidOperationException("发布版本号无效，请使用 v主版本.次版本.修订号 格式。");
        }

        var assembly = Assembly.GetExecutingAssembly();
        var assemblyName = assembly.GetName();
        var currentVersion = assemblyName.Version;
        if (currentVersion is null || remoteVersion <= currentVersion) {
            return null;
        }

        var asset = FindDownloadAsset(release);
        if (asset is null) {
            throw new InvalidOperationException("发现新版本，但发布中缺少 QuickLauncher.exe 便携版。不能使用安装器或压缩包直接覆盖程序。");
        }

        return new UpdateInfo {
            CurrentVersion = currentVersion.ToString(3),
            NewVersion = release.TagName!.TrimStart('v', 'V'),
            Description = release.Body ?? string.Empty,
            DownloadUrl = asset.DownloadUrl,
            DownloadSize = asset.Size,
            Digest = asset.Digest
        };
    }

    public async Task DownloadAndApplyAsync(UpdateInfo info) {
        var installer = new UpdateInstallerService();
        var session = installer.CreateSession();
        try {
            await DownloadAsync(info, session.SourcePath);
            ValidateExecutable(session.SourcePath, info.NewVersion);
            await installer.StartAsync(session);
        }
        catch (Exception exception) {
            installer.RecordFailure(session, exception);
            var reason = exception is OperationCanceledException
                ? "下载更新超时，请检查网络后重试。"
                : exception.Message;
            var message = $"{reason}\n\n更新日志：{session.LogPath}";
            throw new InvalidOperationException(message, exception);
        }
    }

    private async Task DownloadAsync(UpdateInfo info, string destination) {
        var downloadUri = new Uri(info.DownloadUrl, UriKind.Absolute);
        if (downloadUri.Scheme != Uri.UriSchemeHttps) {
            throw new InvalidOperationException("更新下载地址必须使用 HTTPS。");
        }

        var downloadTimeout = TimeSpan.FromMinutes(10);
        using var cancellation = new CancellationTokenSource(downloadTimeout);
        var token = cancellation.Token;
        using var response = await Http.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead, token);
        if (!response.IsSuccessStatusCode) {
            throw new InvalidOperationException($"下载失败：HTTP {(int)response.StatusCode}。");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) || contentType.Contains("json", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("下载返回了网页或错误信息，并非可执行文件。");
        }

        await using (var stream = await response.Content.ReadAsStreamAsync(token))
        await using (var file = File.Create(destination)) {
            await stream.CopyToAsync(file, token);
            var responseSize = response.Content.Headers.ContentLength;
            if ((info.DownloadSize > 0 && file.Length != info.DownloadSize) || (responseSize.HasValue && file.Length != responseSize.Value)) {
                throw new InvalidOperationException("下载文件不完整，文件大小与发布信息不一致。");
            }
        }

        if (!string.IsNullOrWhiteSpace(info.Digest)) {
            const string prefix = "sha256:";
            if (!info.Digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("发布文件使用了不支持的摘要格式。");
            }
            await using var file = File.OpenRead(destination);
            var hash = await SHA256.HashDataAsync(file, token);
            var actualDigest = Convert.ToHexString(hash);
            var expectedDigest = info.Digest[prefix.Length..];
            if (!string.Equals(actualDigest, expectedDigest, StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("下载文件校验失败，文件内容与发布信息不一致。");
            }
        }
    }

    private void ValidateExecutable(string path, string expectedVersion) {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);
        if (stream.Length < 64 || reader.ReadUInt16() != 0x5A4D) {
            throw new InvalidOperationException("下载文件不是有效的 Windows 可执行程序。");
        }
        stream.Position = 0x3C;
        var headerOffset = reader.ReadInt32();
        if (headerOffset < 64 || headerOffset > stream.Length - 6) {
            throw new InvalidOperationException("下载文件的 PE 头无效。");
        }
        stream.Position = headerOffset;
        if (reader.ReadUInt32() != 0x00004550) {
            throw new InvalidOperationException("下载文件的 PE 签名无效。");
        }
        var machine = reader.ReadUInt16();
        var expectedMachine = RuntimeInformation.ProcessArchitecture switch {
            Architecture.X64 => 0x8664,
            Architecture.X86 => 0x014C,
            Architecture.Arm64 => 0xAA64,
            _ => 0
        };
        if (machine != expectedMachine) {
            throw new InvalidOperationException("发布文件的架构与当前程序不一致，请手动下载对应架构的版本。");
        }

        var fileInfo = FileVersionInfo.GetVersionInfo(path);
        var actualVersion = ParseVersion(fileInfo.FileVersion);
        var releaseVersion = ParseVersion(expectedVersion);
        if (actualVersion is null || actualVersion != releaseVersion) {
            throw new InvalidOperationException("发布文件的版本与 Release 标签不一致，请重新发布正确的程序。");
        }
        if (!string.Equals(fileInfo.ProductName, "QuickLauncher", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("发布文件不是 QuickLauncher 主程序，已取消替换。");
        }
    }

    private GitHubAsset? FindDownloadAsset(GitHubRelease release) {
        if (release.Assets is null) {
            return null;
        }
        foreach (var asset in release.Assets) {
            if (string.Equals(asset.Name, "QuickLauncher.exe", StringComparison.OrdinalIgnoreCase)) {
                return asset;
            }
        }
        return null;
    }

    private Version? ParseVersion(string? tagName) {
        if (string.IsNullOrWhiteSpace(tagName)) {
            return null;
        }
        var versionText = tagName.TrimStart('v', 'V');
        if (!Version.TryParse(versionText, out var version) || version.Build < 0) {
            return null;
        }
        var revision = Math.Max(version.Revision, 0);
        // 统一三段标签与程序集中的四段版本。
        var normalized = $"{version.Major}.{version.Minor}.{version.Build}.{revision}";
        return Version.Parse(normalized);
    }

    private sealed class GitHubRelease {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }
        [JsonPropertyName("body")]
        public string? Body { get; set; }
        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        [JsonPropertyName("browser_download_url")]
        public string DownloadUrl { get; set; } = string.Empty;
        [JsonPropertyName("size")]
        public long Size { get; set; }
        [JsonPropertyName("digest")]
        public string? Digest { get; set; }
    }
}

public sealed class UpdateInfo {
    public string CurrentVersion { get; set; } = string.Empty;
    public string NewVersion { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public long DownloadSize { get; set; }
    public string? Digest { get; set; }
}
