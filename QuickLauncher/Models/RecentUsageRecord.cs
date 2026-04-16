namespace QuickLauncher.Models;

public sealed class RecentUsageRecord {
    public string AppId { get; set; } = string.Empty;
    public int LaunchCount { get; set; }
    public DateTime LastLaunchedAt { get; set; }
}
