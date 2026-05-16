using System.IO;
using System.Text;
using QuickLauncher.Models;

namespace QuickLauncher.Services;

public sealed class AppSearchService {
    private readonly SearchPinyinService _pinyinService;
    private readonly RecentUsageService _recentUsageService;
    private readonly AppIconService _iconService;
    private List<PreparedEntry> _entries = [];

    public AppSearchService(
        SearchPinyinService pinyinService,
        RecentUsageService recentUsageService,
        AppIconService iconService) {
        _pinyinService = pinyinService;
        _recentUsageService = recentUsageService;
        _iconService = iconService;
    }

    public void RefreshIndex(IEnumerable<AppEntry> entries) {
        _entries = entries
            .GroupBy(entry => NormalizeDisplayName(entry.DisplayName), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(Prepare)
            .OrderBy(item => item.Entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string NormalizeDisplayName(string text) {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text) {
            if (char.IsLetterOrDigit(character)) {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    public IReadOnlyList<SearchResult> Search(string query, int maxCount = 8) {
        var normalizedQuery = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery)) {
            return GetRecommended(maxCount);
        }

        return _entries
            .Select(entry => Score(entry, normalizedQuery))
            .Where(result => result is not null)
            .Select(result => result!)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.App.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Take(maxCount)
            .ToList();
    }

    private IReadOnlyList<SearchResult> GetRecommended(int maxCount) {
        var records = _recentUsageService.GetAll()
            .OrderByDescending(item => _recentUsageService.GetWeight(item.AppId))
            .ThenByDescending(item => item.LastLaunchedAt)
            .ToList();
        var recommended = new List<SearchResult>();
        var addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in records) {
            var entry = _entries.FirstOrDefault(item => item.Entry.Id == record.AppId);
            if (entry is null || !addedIds.Add(entry.Entry.Id)) {
                continue;
            }

            recommended.Add(ToSearchResult(entry, 500 + _recentUsageService.GetWeight(record.AppId), "最近使用"));
            if (recommended.Count >= maxCount) {
                return recommended;
            }
        }

        foreach (var entry in _entries) {
            if (!addedIds.Add(entry.Entry.Id)) {
                continue;
            }

            recommended.Add(ToSearchResult(entry, 100, "推荐"));
            if (recommended.Count >= maxCount) {
                break;
            }
        }

        return recommended;
    }

    private SearchResult? Score(PreparedEntry entry, string query) {
        var bestScore = 0;
        var reason = string.Empty;

        if (entry.NormalizedName.StartsWith(query, StringComparison.OrdinalIgnoreCase)) {
            bestScore = 1000;
            reason = "名称前缀";
        }
        else if (!string.IsNullOrWhiteSpace(entry.NormalizedExecutableName) &&
                 entry.NormalizedExecutableName.StartsWith(query, StringComparison.OrdinalIgnoreCase)) {
            bestScore = 960;
            reason = "程序名前缀";
        }
        else if (entry.NormalizedName.Contains(query, StringComparison.OrdinalIgnoreCase)) {
            bestScore = 860;
            reason = "名称子串";
        }
        else if (entry.PinyinFull.StartsWith(query, StringComparison.OrdinalIgnoreCase)) {
            bestScore = 830;
            reason = "拼音全拼";
        }
        else if (entry.PinyinInitials.StartsWith(query, StringComparison.OrdinalIgnoreCase)) {
            bestScore = 800;
            reason = "拼音首字母";
        }
        else if (entry.AliasTokens.Any(token => token.StartsWith(query, StringComparison.OrdinalIgnoreCase))) {
            bestScore = 760;
            reason = "别名前缀";
        }
        else if (IsSubsequenceMatch(entry.NormalizedName, query) ||
                 IsSubsequenceMatch(entry.PinyinFull, query) ||
                 IsSubsequenceMatch(entry.PinyinInitials, query)) {
            bestScore = 680;
            reason = "模糊匹配";
        }

        if (bestScore <= 0) {
            return null;
        }

        bestScore += _recentUsageService.GetWeight(entry.Entry.Id);
        return ToSearchResult(entry, bestScore, reason);
    }

    private SearchResult ToSearchResult(PreparedEntry entry, int score, string reason) {
        return new SearchResult {
            App = entry.Entry,
            Score = score,
            MatchReason = reason,
            SecondaryText = GetSecondaryText(entry.Entry),
            PinyinText = string.IsNullOrWhiteSpace(entry.PinyinFull) ? string.Empty : $"拼音: {entry.PinyinFull}",
            SourceText = GetSourceText(entry.Entry.SourceType),
            IconText = GetIconText(entry.Entry),
            IconImage = entry.IconImage
        };
    }

    private PreparedEntry Prepare(AppEntry entry) {
        var aliasTokens = SplitTokens(entry.DisplayName)
            .Concat(SplitTokens(entry.ExecutableName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var pinyin = _pinyinService.Convert(entry.DisplayName);

        return new PreparedEntry {
            Entry = entry,
            NormalizedName = Normalize(entry.DisplayName),
            NormalizedExecutableName = Normalize(entry.ExecutableName),
            AliasTokens = aliasTokens,
            PinyinFull = pinyin.Full,
            PinyinInitials = pinyin.Initials,
            IconImage = _iconService.GetIcon(entry)
        };
    }

    private static string GetSecondaryText(AppEntry entry) {
        if (!string.IsNullOrWhiteSpace(entry.ExecutableName)) {
            return entry.ExecutableName;
        }

        if (entry.LaunchKind == LaunchKind.Uwp) {
            return "UWP 应用";
        }

        if (!string.IsNullOrWhiteSpace(entry.ResolvedTarget)) {
            return Path.GetFileName(entry.ResolvedTarget);
        }

        return Path.GetFileName(entry.LaunchTarget);
    }

    private static string GetSourceText(SourceType sourceType) {
        return sourceType switch {
            SourceType.StartMenu => "开始菜单",
            SourceType.Desktop => "桌面快捷方式",
            SourceType.Uwp => "UWP",
            SourceType.CustomFolder => "自定义目录",
            _ => "未知来源"
        };
    }

    private static string GetIconText(AppEntry entry) {
        return entry.SourceType switch {
            SourceType.StartMenu => "开",
            SourceType.Desktop => "桌",
            SourceType.Uwp => "U",
            SourceType.CustomFolder => "目",
            _ => entry.LaunchKind == LaunchKind.Uwp ? "U" : "App"
        };
    }

    private static string Normalize(string text) {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text) {
            if (char.IsLetterOrDigit(character)) {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static IEnumerable<string> SplitTokens(string text) {
        var builder = new StringBuilder();

        foreach (var character in text) {
            if (char.IsLetterOrDigit(character)) {
                builder.Append(char.ToLowerInvariant(character));
                continue;
            }

            if (builder.Length > 0) {
                yield return builder.ToString();
                builder.Clear();
            }
        }

        if (builder.Length > 0) {
            yield return builder.ToString();
        }
    }

    private static bool IsSubsequenceMatch(string source, string query) {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(query)) {
            return false;
        }

        var sourceIndex = 0;
        var queryIndex = 0;

        while (sourceIndex < source.Length && queryIndex < query.Length) {
            if (source[sourceIndex] == query[queryIndex]) {
                queryIndex++;
            }

            sourceIndex++;
        }

        return queryIndex == query.Length;
    }

    private sealed class PreparedEntry {
        public AppEntry Entry { get; set; } = new();
        public string NormalizedName { get; set; } = string.Empty;
        public string NormalizedExecutableName { get; set; } = string.Empty;
        public List<string> AliasTokens { get; set; } = [];
        public string PinyinFull { get; set; } = string.Empty;
        public string PinyinInitials { get; set; } = string.Empty;
        public System.Windows.Media.ImageSource? IconImage { get; set; }
    }
}
