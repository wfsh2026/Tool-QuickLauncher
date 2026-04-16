namespace QuickLauncher.Models;

public sealed class IndexSummary {
    public int EntryCount { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static IndexSummary Empty() {
        return new IndexSummary();
    }
}
