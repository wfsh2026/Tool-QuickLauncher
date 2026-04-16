using System.Text;

namespace QuickLauncher.Services;

public sealed class SearchPinyinService {
    private static readonly Dictionary<char, string> PinyinMap = new() {
        ['\u5FAE'] = "wei",  ['\u4FE1'] = "xin",  ['\u817E'] = "teng", ['\u8BAF'] = "xun",
        ['\u89C6'] = "shi",  ['\u9891'] = "pin", ['\u7F51'] = "wang", ['\u6613'] = "yi",
        ['\u4E91'] = "yun",  ['\u97F3'] = "yin", ['\u4E50'] = "yue", ['\u5F00'] = "kai",
        ['\u53D1'] = "fa",   ['\u8005'] = "zhe", ['\u5DE5'] = "gong", ['\u5177'] = "ju",
        ['\u8BBE'] = "she",  ['\u7F6E'] = "zhi", ['\u7EC8'] = "zhong", ['\u7AEF'] = "duan",
        ['\u767E'] = "bai",  ['\u5EA6'] = "du",  ['\u5730'] = "di",   ['\u56FE'] = "tu",
        ['\u7535'] = "dian", ['\u8111'] = "nao", ['\u7BA1'] = "guan", ['\u5BB6'] = "jia",
        ['\u98DE'] = "fei",  ['\u4E66'] = "shu", ['\u9499'] = "ding", ['\u4F01'] = "qi",
        ['\u4E1A'] = "ye",   ['\u7248'] = "ban", ['\u56FD'] = "guo",  ['\u9645'] = "ji",
        ['\u684C'] = "zhuo", ['\u9762'] = "mian",['\u8F93'] = "shu",  ['\u5165'] = "ru",
        ['\u6CD5'] = "fa",   ['\u6D4F'] = "liu", ['\u89C8'] = "lan",  ['\u5668'] = "qi",
        ['\u63A7'] = "kong", ['\u5236'] = "zhi", ['\u53F0'] = "tai",  ['\u6587'] = "wen",
        ['\u4EF6'] = "jian", ['\u5939'] = "jia", ['\u7CFB'] = "xi",   ['\u7EDF'] = "tong",
        ['\u52A9'] = "zhu",  ['\u624B'] = "shou",['\u76F8'] = "xiang",['\u673A'] = "ji",
        ['\u90AE'] = "you",  ['\u7BB1'] = "xiang",['\u5546'] = "shang",['\u5E97'] = "dian",
        ['\u4F1A'] = "hui",  ['\u8BAE'] = "yi",  ['\u6D59'] = "zhe",  ['\u91CC'] = "li",
        ['\u6DD8'] = "tao",  ['\u5B9D'] = "bao", ['\u732B'] = "mao",  ['\u7CBE'] = "jing",
        ['\u7075'] = "ling", ['\u526A'] = "jian",['\u6620'] = "ying", ['\u72D0'] = "hu",
        ['\u641C'] = "sou",  ['\u72D7'] = "gou", ['\u5B89'] = "an",   ['\u88C5'] = "zhuang",
        ['\u5378'] = "xie",  ['\u8F7D'] = "zai", ['\u9A71'] = "qu",   ['\u52A8'] = "dong",
        ['\u841D'] = "luo",  ['\u535C'] = "bo",  ['\u5FEB'] = "kuai", ['\u538B'] = "ya",
        ['\u7F29'] = "suo",  ['\u753B'] = "hua", ['\u677F'] = "ban",  ['\u4FBF'] = "bian",
        ['\u7B7E'] = "qian", ['\u8BB0'] = "ji",  ['\u4E8B'] = "shi",  ['\u672C'] = "ben",
        ['\u8BA1'] = "ji",   ['\u7B97'] = "suan",['\u65E5'] = "ri",   ['\u5386'] = "li",
        ['\u7167'] = "zhao", ['\u7247'] = "pian",['\u5E93'] = "ku",   ['\u64AD'] = "bo",
        ['\u653E'] = "fang", ['\u5A92'] = "mei", ['\u4F53'] = "ti",   ['\u670D'] = "fu",
        ['\u52A1'] = "wu",   ['\u5BA2'] = "ke",  ['\u6237'] = "hu",   ['\u6D4B'] = "ce",
        ['\u8BD5'] = "shi",  ['\u7406'] = "li",  ['\u5E73'] = "ping", ['\u8054'] = "lian",
        ['\u7EDC'] = "luo",  ['\u5934'] = "tou", ['\u6761'] = "tiao", ['\u4ECA'] = "jin",
        ['\u59EC'] = "ji",   ['\u756A'] = "fan", ['\u8304'] = "qie",  ['\u7545'] = "chang",
        ['\u8FDE'] = "lian", ['\u7801'] = "ma",  ['\u8868'] = "biao", ['\u683C'] = "ge",
        ['\u76D8'] = "pan",  ['\u9ED1'] = "hei", ['\u795E'] = "shen", ['\u8BDD'] = "hua",
        ['\u539F'] = "yuan", ['\u661F'] = "xing",['\u7A79'] = "qiong",['\u94C1'] = "tie",
        ['\u9053'] = "dao",  ['\u8230'] = "jian",['\u822A'] = "hang", ['\u536B'] = "wei",
        ['\u58EB'] = "shi",  ['\u9E23'] = "ming",['\u6F6E'] = "chao"
    };

    public (string Full, string Initials) Convert(string text) {
        var fullBuilder = new StringBuilder();
        var initialsBuilder = new StringBuilder();

        foreach (var character in text) {
            if (char.IsWhiteSpace(character)) {
                continue;
            }

            if (IsAsciiLetterOrDigit(character)) {
                var normalized = char.ToLowerInvariant(character);
                fullBuilder.Append(normalized);
                initialsBuilder.Append(normalized);
                continue;
            }

            if (PinyinMap.TryGetValue(character, out var pinyin)) {
                fullBuilder.Append(pinyin);
                initialsBuilder.Append(pinyin[0]);
                continue;
            }

            if (char.IsLetterOrDigit(character)) {
                var normalized = char.ToLowerInvariant(character);
                fullBuilder.Append(normalized);
                initialsBuilder.Append(normalized);
            }
        }

        return (fullBuilder.ToString(), initialsBuilder.ToString());
    }

    private static bool IsAsciiLetterOrDigit(char character) {
        return (character >= 'a' && character <= 'z') ||
               (character >= 'A' && character <= 'Z') ||
               (character >= '0' && character <= '9');
    }
}
