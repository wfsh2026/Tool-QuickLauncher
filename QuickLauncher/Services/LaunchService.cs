using System.Diagnostics;
using System.IO;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

public sealed class LaunchService {
    private readonly ProcessActivationService _activationService = new();

    public Task LaunchAsync(AppEntry entry) {
        return Task.Run(() => Launch(entry));
    }

    private void Launch(AppEntry entry) {
        if (entry.LaunchKind != LaunchKind.Uwp && _activationService.TryActivate(entry)) {
            return;
        }

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
