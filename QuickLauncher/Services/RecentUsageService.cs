using System.IO;
using System.Text.Json;
using QuickLauncher.Infrastructure;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

public sealed class RecentUsageService {
    private const int LaunchCountWeight = 25;
    private const int FreshnessDays = 30;
    private const int MaxUsageWeight = 800;

    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly List<RecentUsageRecord> _records;

    public RecentUsageService() : this(Path.Combine(AppDataPaths.RootDirectory, "recent-usage.json")) {
    }

    internal RecentUsageService(string filePath) {
        _filePath = filePath;
        _records = LoadRecords();
    }

    internal RecentUsageService(IEnumerable<RecentUsageRecord> records) {
        _filePath = string.Empty;
        _records = records.ToList();
    }

    public IReadOnlyList<RecentUsageRecord> GetAll() {
        return _records;
    }

    public int GetWeight(string appId) {
        var record = _records.FirstOrDefault(item => item.AppId == appId);
        if (record is null) {
            return 0;
        }

        var freshness = Math.Max(0, FreshnessDays - (int)(DateTime.UtcNow - record.LastLaunchedAt).TotalDays);
        return Math.Min(MaxUsageWeight, record.LaunchCount * LaunchCountWeight + freshness);
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
        if (string.IsNullOrWhiteSpace(_filePath)) {
            return;
        }

        var json = JsonSerializer.Serialize(_records, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
