using QuickLauncher.Models;
using QuickLauncher.Services;

var tests = new (string Name, Action Run)[] {
    ("高频选中文件在匹配结果中优先", FrequentSelectionMovesMatchingFileFirst),
    ("高频选中文件在推荐结果中优先", FrequentSelectionMovesRecommendationFirst),
    ("无匹配输入返回空结果", InvalidQueryReturnsNoResults)
};

var failedCount = 0;
foreach (var test in tests) {
    try {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex) {
        failedCount++;
        Console.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

return failedCount == 0 ? 0 : 1;

static void FrequentSelectionMovesMatchingFileFirst() {
    var recentUsage = new RecentUsageService([
        new RecentUsageRecord {
            AppId = "Alpine",
            LaunchCount = 20,
            LastLaunchedAt = DateTime.UtcNow
        }
    ]);
    var search = CreateSearch(recentUsage);
    search.RefreshIndex([Entry("Alpha"), Entry("Alpine")]);

    var results = search.Search("al", 8);

    AssertEqual("Alpine", results[0].App.DisplayName);
    AssertTrue(results[0].Score > results[1].Score, "高频文件的分数应高于普通文件。");
}

static void FrequentSelectionMovesRecommendationFirst() {
    var recentUsage = new RecentUsageService([
        new RecentUsageRecord {
            AppId = "Alpha",
            LaunchCount = 1,
            LastLaunchedAt = DateTime.UtcNow
        },
        new RecentUsageRecord {
            AppId = "Alpine",
            LaunchCount = 20,
            LastLaunchedAt = DateTime.UtcNow.AddDays(-10)
        }
    ]);
    var search = CreateSearch(recentUsage);
    search.RefreshIndex([Entry("Alpha"), Entry("Alpine")]);

    var results = search.Search("", 8);

    AssertEqual("Alpine", results[0].App.DisplayName);
}

static void InvalidQueryReturnsNoResults() {
    var search = CreateSearch(new RecentUsageService([]));
    search.RefreshIndex([Entry("Alpha")]);

    var results = search.Search("zzzzzz", 8);

    AssertEqual(0, results.Count);
}

static AppSearchService CreateSearch(RecentUsageService recentUsage) {
    return new AppSearchService(new SearchPinyinService(), recentUsage, new AppIconService());
}

static AppEntry Entry(string name) {
    return new AppEntry {
        Id = name,
        DisplayName = name,
        ExecutableName = $"{name}.exe",
        LaunchKind = LaunchKind.Executable,
        LaunchTarget = $@"C:\Apps\{name}.exe",
        SourceType = SourceType.CustomFolder,
        LastIndexedAt = DateTime.UtcNow
    };
}

static void AssertEqual<T>(T expected, T actual) {
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) {
        throw new InvalidOperationException($"期望 {expected}，实际 {actual}。");
    }
}

static void AssertTrue(bool condition, string message) {
    if (!condition) {
        throw new InvalidOperationException(message);
    }
}
