using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace QuickLauncher.Services;

public sealed class UpdateService {
    private const string GitHubApiUrl = "https://api.github.com/repos/wfsh2026/Tool-QuickLauncher/releases/latest";

    private static readonly HttpClient Http = new() {
        DefaultRequestHeaders = {
            { "User-Agent", "QuickLauncher" },
            { "Accept", "application/vnd.github.v3+json" }
        },
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<UpdateInfo?> CheckForUpdateAsync() {
        var response = await Http.GetAsync(GitHubApiUrl);
        if (!response.IsSuccessStatusCode) {
            return null;
        }

        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>();
        if (release is null || string.IsNullOrWhiteSpace(release.TagName)) {
            return null;
        }

        var remoteVersion = ParseVersion(release.TagName);
        if (remoteVersion is null) {
            return null;
        }

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
        if (currentVersion is null || remoteVersion <= currentVersion) {
            return null;
        }

        var downloadUrl = FindDownloadUrl(release);
        if (downloadUrl is null) {
            return null;
        }

        return new UpdateInfo {
            CurrentVersion = currentVersion.ToString(3),
            NewVersion = remoteVersion.ToString(3),
            Description = release.Body ?? string.Empty,
            DownloadUrl = downloadUrl
        };
    }

    public async Task DownloadAndApplyAsync(UpdateInfo info) {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) {
            throw new InvalidOperationException("无法获取当前进程路径。");
        }

        var exeDir = Path.GetDirectoryName(exePath)!;
        var exeName = Path.GetFileName(exePath);
        var tempDir = Path.Combine(exeDir, ".update_temp");
        if (Directory.Exists(tempDir)) {
            try { Directory.Delete(tempDir, true); } catch { }
        }
        Directory.CreateDirectory(tempDir);

        var tempExePath = Path.Combine(tempDir, exeName);

        // 先通过 HEAD/GET 校验下载响应，避免把 404 HTML 或 JSON 当作 exe 落盘。
        using (var response = await Http.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead)) {
            if (!response.IsSuccessStatusCode) {
                throw new InvalidOperationException($"下载失败：服务器返回 HTTP {(int)response.StatusCode}。");
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("下载失败：返回的内容是网页而非可执行文件，请检查发布资产配置。");
            }

            await using (var stream = await response.Content.ReadAsStreamAsync())
            using (var fileStream = File.Create(tempExePath)) {
                await stream.CopyToAsync(fileStream);
            }
        }

        ValidatePortableExecutable(tempExePath);

        var scriptPath = Path.Combine(tempDir, "update.bat");
        var logPath = Path.Combine(exeDir, ".update_error.log");
        var script = $"""
            @echo off
            chcp 65001 >nul
            set "SRC={tempExePath}"
            set "DST={exePath}"
            set "LOG={logPath}"
            echo [%date% %time%] 开始更新 > "%LOG%"

            REM 等待主程序退出后重试复制，最多约 15 秒。
            set /a TRIES=0
            :COPY_LOOP
            copy /y "%SRC%" "%DST%" >nul 2>&1
            if errorlevel 1 (
                set /a TRIES+=1
                if %TRIES% GEQ 30 (
                    echo [%date% %time%] 复制失败，已重试 %%TRIES%% 次 >> "%LOG%"
                    echo 更新失败：无法替换主程序文件，请手动替换。>> "%LOG%"
                    start "" notepad "%LOG%"
                    exit /b 1
                )
                timeout /t 1 /nobreak >nul
                goto COPY_LOOP
            )

            echo [%date% %time%] 复制成功，正在重启 >> "%LOG%"
            start "" "%DST%"
            rmdir /s /q "{tempDir}"
            del "%LOG%" 2>nul
            """;
        // 用 UTF-8 with BOM 写入脚本，配合 chcp 65001 才能正确处理中文/空格路径。
        await File.WriteAllTextAsync(scriptPath, script, new System.Text.UTF8Encoding(true));

        Process.Start(new ProcessStartInfo {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    private static void ValidatePortableExecutable(string path) {
        if (!File.Exists(path)) {
            throw new InvalidOperationException("下载失败：文件未正确写入。");
        }

        var info = new FileInfo(path);
        if (info.Length < 64) {
            throw new InvalidOperationException("下载失败：文件过小，不是有效的可执行文件（可能下载到错误内容）。");
        }

        using var stream = File.OpenRead(path);
        var mzw = stream.ReadByte();
        var mzb = stream.ReadByte();
        if (mzw != 'M' || mzb != 'Z') {
            throw new InvalidOperationException("下载失败：文件不是有效的可执行文件（缺少 PE 头），请检查发布资产是否为单个 .exe。");
        }
    }

    private static string? FindDownloadUrl(GitHubRelease release) {
        if (release.Assets is null || release.Assets.Count == 0) {
            return null;
        }

        var exeAssets = release.Assets
            .Where(asset => asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (exeAssets.Count == 0) {
            // 没有任何 .exe 资产时不做盲目下载（避免把源码包/zip 当 exe）。
            return null;
        }

        var currentName = Path.GetFileName(Environment.ProcessPath ?? string.Empty);
        if (!string.IsNullOrEmpty(currentName)) {
            var matching = exeAssets.FirstOrDefault(asset =>
                string.Equals(asset.Name, currentName, StringComparison.OrdinalIgnoreCase));
            if (matching is not null) {
                return matching.DownloadUrl;
            }
        }

        return exeAssets[0].DownloadUrl;
    }

    private static Version? ParseVersion(string tagName) {
        var versionText = tagName.TrimStart('v', 'V');
        return Version.TryParse(versionText, out var version) ? version : null;
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
    }
}

public sealed class UpdateInfo {
    public string CurrentVersion { get; set; } = string.Empty;
    public string NewVersion { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}
