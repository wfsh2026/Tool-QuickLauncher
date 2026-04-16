namespace QuickLauncher.Models;

public sealed class AppEntry {
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ExecutableName { get; set; } = string.Empty;
    public LaunchKind LaunchKind { get; set; }
    public string LaunchTarget { get; set; } = string.Empty;
    public string ResolvedTarget { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public SourceType SourceType { get; set; }
    public DateTime LastIndexedAt { get; set; }
}
