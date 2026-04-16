using System.IO;
using System.Text.Json;
using QuickLauncher.Infrastructure;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

public sealed class AppIndexService {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true
    };

    private readonly AppDiscoveryService _discoveryService = new();
    private IndexSnapshot _snapshot = new() {
        UpdatedAt = DateTime.UtcNow
    };

    public IReadOnlyList<AppEntry> Entries => _snapshot.Entries;

    public IndexSummary GetSummary() {
        return _snapshot.ToSummary();
    }

    public async Task<IndexSummary> LoadAsync(UserSettings settings) {
        if (File.Exists(AppDataPaths.AppIndexFilePath)) {
            try {
                var json = await File.ReadAllTextAsync(AppDataPaths.AppIndexFilePath);
                var snapshot = JsonSerializer.Deserialize<IndexSnapshot>(json, JsonOptions);
                if (snapshot is not null) {
                    _snapshot = snapshot;
                    return _snapshot.ToSummary();
                }
            }
            catch {
            }
        }

        return await RebuildAsync(settings);
    }

    public async Task<IndexSummary> RebuildAsync(UserSettings settings) {
        var entries = await _discoveryService.DiscoverAsync(settings);
        _snapshot = new IndexSnapshot {
            UpdatedAt = DateTime.UtcNow,
            Entries = [.. entries]
        };

        var json = JsonSerializer.Serialize(_snapshot, JsonOptions);
        await File.WriteAllTextAsync(AppDataPaths.AppIndexFilePath, json);
        return _snapshot.ToSummary();
    }
}
