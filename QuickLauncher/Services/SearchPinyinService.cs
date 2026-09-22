namespace QuickLauncher.Services;

public sealed class SearchPinyinService {
    private readonly PinyinService _pinyinService = new();

    public (string Full, string Initials) Convert(string text) {
        return _pinyinService.Convert(text);
    }
}
