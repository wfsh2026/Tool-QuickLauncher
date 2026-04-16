using System.Windows.Media;

namespace QuickLauncher.Models;

public sealed class SearchResult {
    public AppEntry App { get; set; } = new();
    public int Score { get; set; }
    public string MatchReason { get; set; } = string.Empty;
    public string SecondaryText { get; set; } = string.Empty;
    public string PinyinText { get; set; } = string.Empty;
    public string SourceText { get; set; } = string.Empty;
    public string IconText { get; set; } = string.Empty;
    public ImageSource? IconImage { get; set; }
}
