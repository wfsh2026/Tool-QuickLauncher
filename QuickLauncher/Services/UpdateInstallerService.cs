using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using QuickLauncher.Infrastructure;

namespace QuickLauncher.Services;

internal sealed class UpdateInstallerService {
    public UpdateSession CreateSession() {
        var targetPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(targetPath)) {
            throw new InvalidOperationException("无法获取当前程序路径。");
        }
        var directory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(directory)) {
            throw new InvalidOperationException("无法获取当前程序目录。");
        }

        var id = Guid.NewGuid();
        var sessionName = id.ToString("N");
        var sessionDirectory = Path.Combine(AppDataPaths.RootDirectory, "updates", sessionName);
        Directory.CreateDirectory(sessionDirectory);
        using var parent = Process.GetCurrentProcess();
        var parentStartTime = parent.StartTime;
        var parentFileTime = parentStartTime.ToFileTimeUtc();
        var session = new UpdateSession {
            DirectoryPath = sessionDirectory,
            SourcePath = Path.Combine(sessionDirectory, "QuickLauncher.exe"),
            TargetPath = targetPath,
            StagedPath = Path.Combine(directory, $".QuickLauncher-update-{sessionName}.exe"),
            BackupPath = Path.Combine(directory, $"QuickLauncher-{sessionName}.backup"),
            LogPath = Path.Combine(sessionDirectory, "update.log"),
            ReadyPath = Path.Combine(sessionDirectory, "ready"),
            ParentProcessId = parent.Id,
            ParentStartTime = parentFileTime.ToString()
        };
        try {
            // 提前检查目录写权限；最终替换仍可能被其他实例或安全软件阻止。
            using (var probe = File.Create(session.StagedPath)) {
                probe.Flush();
            }
            File.Delete(session.StagedPath);
            return session;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException) {
            RecordFailure(session, exception);
            var message = $"程序目录无法写入，请将便携版移动到有写权限的目录，或退出后手动替换。\n更新日志：{session.LogPath}";
            throw new InvalidOperationException(message, exception);
        }
    }

    public async Task StartAsync(UpdateSession session) {
        var assembly = Assembly.GetExecutingAssembly();
        using var resource = assembly.GetManifestResourceStream("QuickLauncher.UpdateInstaller.ps1");
        if (resource is null) {
            throw new InvalidOperationException("程序缺少更新组件，请重新下载完整版本。");
        }
        using var reader = new StreamReader(resource);
        var script = await reader.ReadToEndAsync();
        var scriptPath = Path.Combine(session.DirectoryPath, "install.ps1");
        var scriptEncoding = new UTF8Encoding(true);
        await File.WriteAllTextAsync(scriptPath, script, scriptEncoding);

        await using (var source = File.OpenRead(session.SourcePath)) {
            var hash = await SHA256.HashDataAsync(source);
            session.SourceHash = Convert.ToHexString(hash);
        }
        var configuration = JsonSerializer.Serialize(session);
        var configurationPath = Path.Combine(session.DirectoryPath, "session.json");
        await File.WriteAllTextAsync(configurationPath, configuration);

        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var powershellPath = Path.Combine(systemDirectory, "WindowsPowerShell", @"v1.0\powershell.exe");
        var startInfo = new ProcessStartInfo {
            FileName = powershellPath,
            WorkingDirectory = session.DirectoryPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);

        using var process = Process.Start(startInfo);
        if (process is null) {
            throw new InvalidOperationException("无法启动独立更新进程，当前程序已保留。");
        }
        try {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline) {
                if (process.HasExited) {
                    throw new InvalidOperationException("更新进程提前退出，请检查更新日志或系统是否限制 PowerShell 脚本运行。");
                }
                if (File.Exists(session.ReadyPath)) {
                    return;
                }
                await Task.Delay(100);
            }
            throw new InvalidOperationException("更新进程启动超时，当前程序已保留。");
        }
        catch {
            // 未交接成功时阻止更新器在用户稍后退出程序时继续替换。
            try {
                if (!process.HasExited) {
                    process.Kill();
                }
            }
            catch (InvalidOperationException) {
                // 更新进程可能刚好自行退出。
            }
            throw;
        }
    }

    public void RecordFailure(UpdateSession session, Exception exception) {
        try {
            var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 更新准备失败\n{exception}\n";
            File.AppendAllText(session.LogPath, message);
        }
        catch {
            // 日志写入失败不能覆盖原始下载、权限或启动异常。
        }
    }
}

internal sealed class UpdateSession {
    public string DirectoryPath { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string StagedPath { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public string LogPath { get; set; } = string.Empty;
    public string ReadyPath { get; set; } = string.Empty;
    public string ParentStartTime { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public int ParentProcessId { get; set; }
}
