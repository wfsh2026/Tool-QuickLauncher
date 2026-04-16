using System.IO;
using System.Text.Json;
using QuickLauncher.Infrastructure;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

public sealed class RecentUsageService {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true
    };

    private readonly string _filePath = Path.Combine(AppDataPaths.RootDirectory, "recent-usage.json");
    private readonly List<RecentUsageRecord> _records;

    public RecentUsageService() {
        _records = LoadRecords();
    }

    public IReadOnlyList<RecentUsageRecord> GetAll() {
        return _records;
    }

    public int GetWeight(string appId) {
        var record = _records.FirstOrDefault(item => item.AppId == appId);
        if (record is null) {
            return 0;
        }

        var freshness = Math.Max(0, 30 - (int)(DateTime.UtcNow - record.LastLaunchedAt).TotalDays);
        return Math.Min(60, record.LaunchCount * 4 + freshness);
    }

    public void RecordLaunch(string appId) {
        var record = _records.FirstOrDefault(item => item.AppId == appId);
        if (record is null) {
            _records.Add(new RecentUsageRecord {
                AppId = appId,
                LaunchCount = 1,
                LastLaunchedAt = DateTime.UtcNow
            });
        }
        else {
            record.LaunchCount += 1;
            record.LastLaunchedAt = DateTime.UtcNow;
        }

        Save();
    }

    private List<RecentUsageRecord> LoadRecords() {
        if (!File.Exists(_filePath)) {
            return [];
        }

        try {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<RecentUsageRecord>>(json, JsonOptions) ?? [];
        }
        catch {
            return [];
        }
    }

    private void Save() {
        var json = JsonSerializer.Serialize(_records, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
