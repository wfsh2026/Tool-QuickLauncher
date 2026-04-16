using Microsoft.Win32;

namespace QuickLauncher.Services;

public sealed class StartupService {
    private const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "QuickLauncher";

    public void Apply(bool enabled) {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunRegistryPath, writable: true)
                           ?? Registry.CurrentUser.CreateSubKey(RunRegistryPath);

        if (runKey is null) {
            return;
        }

        if (!enabled) {
            runKey.DeleteValue(AppName, throwOnMissingValue: false);
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath)) {
            return;
        }

        runKey.SetValue(AppName, $"\"{executablePath}\"");
    }
}
