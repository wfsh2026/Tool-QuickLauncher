using System.Text;
using ToolGood.Words.Pinyin;

namespace QuickLauncher.Services;

public sealed class PinyinService {
    public (string Full, string Initials) Convert(string text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return (string.Empty, string.Empty);
        }

        // 整段转换以使用词组读音，避免逐字转换误读音乐、重庆等多音词。
        var normalizedText = text.Normalize(NormalizationForm.FormKC);
        var fullPinyin = WordsHelper.GetPinyin(normalizedText);
        var firstPinyin = WordsHelper.GetFirstPinyin(normalizedText);
        var full = Normalize(fullPinyin);
        var initials = Normalize(firstPinyin);
        return (full, initials);
    }

    private string Normalize(string text) {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text) {
            if (!char.IsLetterOrDigit(character)) {
                continue;
            }

            var normalized = char.ToLowerInvariant(character);
            builder.Append(normalized);
        }
        return builder.ToString();
    }
}
