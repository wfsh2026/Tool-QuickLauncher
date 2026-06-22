using System.IO;
using System.Text.Json;
using QuickLauncher.Infrastructure;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

public sealed class HiddenIndexService {
    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly List<HiddenEntry> _entries;
    private readonly HashSet<string> _keys;

    public HiddenIndexService() : this(AppDataPaths.HiddenEntriesFilePath) {
    }

    internal HiddenIndexService(string filePath) {
        _filePath = filePath;
        _entries = LoadEntries();
        _keys = new HashSet<string>(
            _entries.Select(item => item.Key),
            StringComparer.OrdinalIgnoreCase);
    }

    internal HiddenIndexService(IEnumerable<HiddenEntry> entries) {
        _filePath = string.Empty;
        _entries = entries.ToList();
        _keys = new HashSet<string>(
            _entries.Select(item => item.Key),
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<HiddenEntry> GetAll() {
        return _entries;
    }

    public bool IsHidden(AppEntry entry) {
        var key = AppDiscoveryService.BuildDeduplicateKey(entry);
        return _keys.Contains(key);
    }

    public bool IsHiddenByKey(string key) {
        return _keys.Contains(key);
    }

    public void Hide(AppEntry entry, string sourceText = "") {
        var key = AppDiscoveryService.BuildDeduplicateKey(entry);
        if (_keys.Contains(key)) {
            return;
        }

        _entries.Add(new HiddenEntry {
            Key = key,
            DisplayName = entry.DisplayName,
            SourceText = sourceText,
            HiddenAt = DateTime.UtcNow
        });
        _keys.Add(key);
        Save();
    }

    public bool Unhide(string key) {
        var index = _entries.FindIndex(item =>
            string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        if (index < 0) {
            return false;
        }

        _entries.RemoveAt(index);
        _keys.RemoveWhere(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase));
        Save();
        return true;
    }

    private List<HiddenEntry> LoadEntries() {
        if (!File.Exists(_filePath)) {
            return [];
        }

        try {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<HiddenEntry>>(json, JsonOptions) ?? [];
        }
        catch {
            return [];
        }
    }

    private void Save() {
        if (string.IsNullOrWhiteSpace(_filePath)) {
            return;
        }

        try {
            var json = JsonSerializer.Serialize(_entries, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch {
            // 持久化失败不应阻断搜索/隐藏流程，内存状态仍然有效。
        }
    }
}
