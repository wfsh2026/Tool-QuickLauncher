using System.IO;

namespace QuickLauncher.Infrastructure;

internal static class AppDataPaths {
    static AppDataPaths() {
        RootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickLauncher");
        Directory.CreateDirectory(RootDirectory);
    }

    public static string RootDirectory { get; }

    public static string SettingsFilePath => Path.Combine(RootDirectory, "settings.json");

    public static string AppIndexFilePath => Path.Combine(RootDirectory, "app-index.json");

    public static string HiddenEntriesFilePath => Path.Combine(RootDirectory, "hidden-entries.json");
}
