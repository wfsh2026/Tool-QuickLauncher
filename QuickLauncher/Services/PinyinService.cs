using System.Text;

namespace QuickLauncher.Services;

public sealed class PinyinService {
    private static readonly Dictionary<char, string> PinyinMap = new() {
        ['微'] = "wei", ['信'] = "xin", ['腾'] = "teng", ['讯'] = "xun",
        ['视'] = "shi", ['频'] = "pin", ['网'] = "wang", ['易'] = "yi",
        ['云'] = "yun", ['音'] = "yin", ['乐'] = "yue", ['开'] = "kai",
        ['发'] = "fa", ['者'] = "zhe", ['工'] = "gong", ['具'] = "ju",
        ['设'] = "she", ['置'] = "zhi", ['终'] = "zhong", ['端'] = "duan",
        ['百'] = "bai", ['度'] = "du", ['地'] = "di", ['图'] = "tu",
        ['电'] = "dian", ['脑'] = "nao", ['管'] = "guan", ['家'] = "jia",
        ['飞'] = "fei", ['书'] = "shu", ['钉'] = "ding", ['企'] = "qi",
        ['业'] = "ye", ['版'] = "ban", ['国'] = "guo", ['际'] = "ji",
        ['桌'] = "zhuo", ['面'] = "mian", ['输'] = "shu", ['入'] = "ru",
        ['法'] = "fa", ['浏'] = "liu", ['览'] = "lan", ['器'] = "qi",
        ['控'] = "kong", ['制'] = "zhi", ['台'] = "tai", ['文'] = "wen",
        ['件'] = "jian", ['夹'] = "jia", ['系'] = "xi", ['统'] = "tong",
        ['助'] = "zhu", ['手'] = "shou", ['相'] = "xiang", ['机'] = "ji",
        ['邮'] = "you", ['箱'] = "xiang", ['商'] = "shang", ['店'] = "dian",
        ['会'] = "hui", ['议'] = "yi", ['浙'] = "zhe", ['里'] = "li",
        ['淘'] = "tao", ['宝'] = "bao", ['猫'] = "mao", ['精'] = "jing",
        ['灵'] = "ling", ['剪'] = "jian", ['映'] = "ying", ['狐'] = "hu",
        ['搜'] = "sou", ['狗'] = "gou", ['安'] = "an", ['装'] = "zhuang",
        ['卸'] = "xie", ['载'] = "zai", ['驱'] = "qu", ['动'] = "dong",
        ['萝'] = "luo", ['卜'] = "bo", ['快'] = "kuai", ['压'] = "ya",
        ['缩'] = "suo", ['画'] = "hua", ['板'] = "ban", ['便'] = "bian",
        ['签'] = "qian", ['记'] = "ji", ['事'] = "shi", ['本'] = "ben",
        ['计'] = "ji", ['算'] = "suan", ['器'] = "qi", ['日'] = "ri",
        ['历'] = "li", ['照'] = "zhao", ['片'] = "pian", ['库'] = "ku",
        ['播'] = "bo", ['放'] = "fang", ['媒'] = "mei", ['体'] = "ti",
        ['服'] = "fu", ['务'] = "wu", ['客'] = "ke", ['户'] = "hu",
        ['测'] = "ce", ['试'] = "shi", ['理'] = "li",
        ['平'] = "ping", ['联'] = "lian", ['络'] = "luo", ['头'] = "tou",
        ['条'] = "tiao", ['今'] = "jin", ['姬'] = "ji", ['番'] = "fan", ['茄'] = "qie",
        ['畅'] = "chang", ['连'] = "lian", ['码'] = "ma", ['表'] = "biao",
        ['格'] = "ge", ['盘'] = "pan", ['黑'] = "hei", ['神'] = "shen",
        ['话'] = "hua", ['原'] = "yuan", ['神'] = "shen", ['星'] = "xing",
        ['穹'] = "qiong", ['铁'] = "tie", ['道'] = "dao", ['舰'] = "jian",
        ['导'] = "dao", ['航'] = "hang", ['卫'] = "wei", ['士'] = "shi"
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
