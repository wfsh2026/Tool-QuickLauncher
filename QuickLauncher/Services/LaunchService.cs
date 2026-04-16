using System.Diagnostics;
using System.IO;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

public sealed class LaunchService {
    public Task LaunchAsync(AppEntry entry) {
        return Task.Run(() => Launch(entry));
    }

    private static void Launch(AppEntry entry) {
        var startInfo = entry.LaunchKind switch {
            LaunchKind.Uwp => new ProcessStartInfo {
                FileName = "explorer.exe",
                Arguments = $"shell:AppsFolder\\{entry.LaunchTarget}",
                UseShellExecute = true
            },
            _ => new ProcessStartInfo {
                FileName = entry.LaunchTarget,
                Arguments = entry.Arguments,
                UseShellExecute = true,
                WorkingDirectory = ResolveWorkingDirectory(entry)
            }
        };

        Process.Start(startInfo);
    }

    private static string ResolveWorkingDirectory(AppEntry entry) {
        if (entry.LaunchKind == LaunchKind.Shortcut && !string.IsNullOrWhiteSpace(entry.ResolvedTarget)) {
            var directory = Path.GetDirectoryName(entry.ResolvedTarget);
            return directory ?? string.Empty;
        }

        if (entry.LaunchKind == LaunchKind.Executable) {
            var directory = Path.GetDirectoryName(entry.LaunchTarget);
            return directory ?? string.Empty;
        }

        return string.Empty;
    }
}
