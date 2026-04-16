namespace QuickLauncher.Models;

public sealed class IndexSnapshot {
    public int SchemaVersion { get; set; } = 1;
    public DateTime UpdatedAt { get; set; }
    public List<AppEntry> Entries { get; set; } = [];

    public IndexSummary ToSummary() {
        return new IndexSummary {
            EntryCount = Entries.Count,
            UpdatedAt = UpdatedAt
        };
    }
}
