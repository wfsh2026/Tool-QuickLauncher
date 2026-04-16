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
        Directory.CreateDirectory(tempDir);

        var tempExePath = Path.Combine(tempDir, exeName);

        using (var stream = await Http.GetStreamAsync(info.DownloadUrl))
        using (var fileStream = File.Create(tempExePath)) {
            await stream.CopyToAsync(fileStream);
        }

        var scriptPath = Path.Combine(tempDir, "update.bat");
        var script = $"""
            @echo off
            chcp 65001 >nul
            timeout /t 2 /nobreak >nul
            copy /y "{tempExePath}" "{exePath}"
            if errorlevel 1 (
                echo 更新失败，请手动替换文件。
                pause
                exit /b 1
            )
            start "" "{exePath}"
            rmdir /s /q "{tempDir}"
            """;
        await File.WriteAllTextAsync(scriptPath, script, System.Text.Encoding.Default);

        Process.Start(new ProcessStartInfo {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            CreateNoWindow = true,
            UseShellExecute = false
        });
    }

    private static string? FindDownloadUrl(GitHubRelease release) {
        if (release.Assets is null || release.Assets.Count == 0) {
            return null;
        }

        foreach (var asset in release.Assets) {
            if (asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) {
                return asset.DownloadUrl;
            }
        }

        return release.Assets[0].DownloadUrl;
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
