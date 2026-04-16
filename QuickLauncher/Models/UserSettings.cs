namespace QuickLauncher.Models;

public sealed class UserSettings {
    public HotkeyGesture Hotkey { get; set; } = HotkeyGesture.CreateDefault();
    public bool RunAtStartup { get; set; }
    public bool HideAfterLaunch { get; set; } = true;
    public bool SearchStartMenu { get; set; } = true;
    public bool SearchDesktop { get; set; } = true;
    public bool SearchUwp { get; set; } = true;
    public List<string> CustomFolders { get; set; } = [];

    public static UserSettings CreateDefault() {
        return new UserSettings();
    }

    public UserSettings Clone() {
        return new UserSettings {
            Hotkey = Hotkey.Clone(),
            RunAtStartup = RunAtStartup,
            HideAfterLaunch = HideAfterLaunch,
            SearchStartMenu = SearchStartMenu,
            SearchDesktop = SearchDesktop,
            SearchUwp = SearchUwp,
            CustomFolders = [.. CustomFolders]
        };
    }
}
