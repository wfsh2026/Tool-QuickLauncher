namespace QuickLauncher.Models;

public sealed class HiddenEntry {
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SourceText { get; set; } = string.Empty;
    public DateTime HiddenAt { get; set; } = DateTime.UtcNow;
}
