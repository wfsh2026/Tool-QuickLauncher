using System.Diagnostics;
using System.IO;
using QuickLauncher.Infrastructure;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

public sealed class ProcessActivationService {
    private static readonly string CurrentProcessName = Process.GetCurrentProcess().ProcessName;

    public bool TryActivate(AppEntry entry) {
        if (entry.LaunchKind == LaunchKind.Uwp) {
            return false;
        }

        var executableName = GetExecutableName(entry);
        if (string.IsNullOrWhiteSpace(executableName)) {
            return false;
        }

        var process = FindProcess(executableName);
        if (process is null) {
            return false;
        }

        try {
            var handle = process.MainWindowHandle;
            if (handle == IntPtr.Zero) {
                return false;
            }

            if (NativeMethods.IsIconic(handle)) {
                NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
            }

            return NativeMethods.SetForegroundWindow(handle);
        }
        catch {
            return false;
        }
    }

    private static string GetExecutableName(AppEntry entry) {
        if (!string.IsNullOrWhiteSpace(entry.ExecutableName)) {
            return entry.ExecutableName;
        }

        var target = !string.IsNullOrWhiteSpace(entry.ResolvedTarget)
            ? entry.ResolvedTarget
            : entry.LaunchTarget;

        return string.IsNullOrWhiteSpace(target) ? string.Empty : Path.GetFileName(target);
    }

    private static Process? FindProcess(string executableName) {
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(executableName);
        if (string.IsNullOrWhiteSpace(nameWithoutExtension)) {
            return null;
        }

        Process[] processes;
        try {
            processes = Process.GetProcessesByName(nameWithoutExtension);
        }
        catch {
            return null;
        }

        foreach (var process in processes) {
            if (string.Equals(process.ProcessName, CurrentProcessName, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            return process;
        }

        return null;
    }
}
