using System.IO;
using System.Text.Json;
using QuickLauncher.Infrastructure;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

public sealed class SettingsService {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true
    };

    private readonly string _settingsFilePath = AppDataPaths.SettingsFilePath;

    public UserSettings Load() {
        if (!File.Exists(_settingsFilePath)) {
            return UserSettings.CreateDefault();
        }

        try {
            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<UserSettings>(json, JsonOptions) ?? UserSettings.CreateDefault();
        }
        catch {
            return UserSettings.CreateDefault();
        }
    }

    public void Save(UserSettings settings) {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsFilePath, json);
    }
}
