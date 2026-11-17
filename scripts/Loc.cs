using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 多语言支持（15 种语言）。
///
/// 约定：代码里继续写简体中文原文当 key。UI 建完后调用 <see cref="TranslateTree"/>，
/// 按「上一门语言的文本 → key → 目标语言文本」整树替换 Label / Button 的文字，
/// 这样新增文案只改 LocTable，不需要给每个控件存引用。
/// 动态拼接的文本（体重、死亡原因等）在生成处调用 T / F。
/// </summary>
public static class Loc
{
    public enum Id
    {
        ZhHans = 0, ZhHant, En, Fr, It, De, EsEs, Ja, Ko, Pl, PtBr, Ru, Es419, Th, Tr
    }

    /// <summary>码 / 选择器里的全名 / 主菜单小按钮上的短名。</summary>
    public static readonly (string Code, string Name, string Short)[] All =
    {
        ("zh_CN", "简体中文", "简体"),
        ("zh_TW", "繁體中文", "繁體"),
        ("en",    "English", "English"),
        ("fr",    "Français", "Français"),
        ("it",    "Italiano", "Italiano"),
        ("de",    "Deutsch", "Deutsch"),
        ("es_ES", "Español (España)", "Español"),
        ("ja",    "日本語", "日本語"),
        ("ko",    "한국어", "한국어"),
        ("pl",    "Polski", "Polski"),
        ("pt_BR", "Português (Brasil)", "Português"),
        ("ru",    "Русский", "Русский"),
        ("es_419", "Español (LatAm)", "Español+"),
        ("th",    "ไทย", "ไทย"),
        ("tr",    "Türkçe", "Türkçe"),
    };

    public static int Count => All.Length;

    private const string SavePath = "user://settings.cfg";
    private const string SaveSection = "ui";
    private const string SaveKey = "language";

    public static Id Current { get; private set; } = Id.ZhHans;

    /// <summary>语言切换后触发，UI 用它刷新动态文本。</summary>
    public static event Action Changed;

    // key(简体原文) -> 各语言文本，下标 = Id
    private static readonly Dictionary<string, string[]> ByKey = Build();
    // 每个语言自己的「文本 -> key」反查表
    private static readonly Dictionary<string, string>[] Reverse = BuildReverse();

    /// <summary>当前 UI 上文字所用的语言（用于反查来源）。</summary>
    private static Id _rendered = Id.ZhHans;

    private static Dictionary<string, string>[] TableSources() => new[]
    {
        LocTable.ZhHans, LocTable.ZhHant, LocTable.En, LocTable.Fr, LocTable.It,
        LocTable.De, LocTable.EsEs, LocTable.Ja, LocTable.Ko, LocTable.Pl,
        LocTable.PtBr, LocTable.Ru, LocTable.Es419, LocTable.Th, LocTable.Tr,
    };

    private static Dictionary<string, string[]> Build()
    {
        var sources = TableSources();
        var map = new Dictionary<string, string[]>(LocTable.ZhHans.Count);
        foreach (string key in LocTable.ZhHans.Keys)
        {
            var row = new string[sources.Length];
            for (int i = 0; i < sources.Length; i++)
                row[i] = sources[i].TryGetValue(key, out string v) && !string.IsNullOrEmpty(v)
                    ? v
                    : LocTable.ZhHans[key];   // 缺项回退简体中文
            map[key] = row;
        }
        return map;
    }

    private static Dictionary<string, string>[] BuildReverse()
    {
        var rev = new Dictionary<string, string>[Count];
        for (int i = 0; i < Count; i++) rev[i] = new Dictionary<string, string>();
        foreach (KeyValuePair<string, string[]> kv in ByKey)
        {
            for (int i = 0; i < Count; i++)
            {
                string text = kv.Value[i];
                if (string.IsNullOrEmpty(text)) continue;
                // 同一语言里不同 key 撞了同一文本时保留先出现的，避免反查抖动
                if (!rev[i].ContainsKey(text)) rev[i][text] = kv.Key;
            }
        }
        return rev;
    }

    /// <summary>启动时调用一次：读存档，没存过就按系统语言匹配最近的一门。</summary>
    public static void Init()
    {
        // UI 代码里的字面量永远是简体中文，所以 _rendered 的起点固定为 ZhHans
        _rendered = Id.ZhHans;

        var cf = new ConfigFile();
        if (cf.Load(SavePath) == Error.Ok)
        {
            string saved = cf.GetValue(SaveSection, SaveKey, "").AsString();
            int idx = Array.FindIndex(All, a => a.Code == saved);
            if (idx >= 0) { Current = (Id)idx; return; }
        }
        Current = GuessFromSystem();
    }

    private static Id GuessFromSystem()
    {
        string loc = OS.GetLocale().Replace('-', '_').ToLowerInvariant();
        string lang = loc.Split('_')[0];

        switch (lang)
        {
            case "zh":
                return loc.Contains("tw") || loc.Contains("hk") || loc.Contains("mo")
                    ? Id.ZhHant : Id.ZhHans;
            case "en": return Id.En;
            case "fr": return Id.Fr;
            case "it": return Id.It;
            case "de": return Id.De;
            case "ja": return Id.Ja;
            case "ko": return Id.Ko;
            case "pl": return Id.Pl;
            case "ru": return Id.Ru;
            case "th": return Id.Th;
            case "tr": return Id.Tr;
            case "pt": return Id.PtBr;
            case "es": return loc.Contains("es_419") || loc.Contains("mx") || loc.Contains("ar")
                || loc.Contains("419") ? Id.Es419 : Id.EsEs;
            default: return Id.En;
        }
    }

    public static void Set(Id id)
    {
        if (Current == id) return;
        Current = id;
        Save();
        Changed?.Invoke();
    }

    private static void Save()
    {
        var cf = new ConfigFile();
        cf.Load(SavePath);                        // 保留同文件里的其它设置
        cf.SetValue(SaveSection, SaveKey, All[(int)Current].Code);
        cf.Save(SavePath);
    }

    /// <summary>取当前语言的文本；表里没有就原样返回。</summary>
    public static string T(string key) =>
        ByKey.TryGetValue(key, out string[] row) ? row[(int)Current] : key;

    /// <summary>带占位符的文本，格式串本身也会被翻译。</summary>
    public static string F(string keyFormat, params object[] args) =>
        string.Format(T(keyFormat), args);

    public static string NativeName(Id id) => All[(int)id].Name;
    public static string ShortName => All[(int)Current].Short;

    /// <summary>递归替换控件树里命中词表的 Label / Button 文本。</summary>
    public static void TranslateTree(Node root)
    {
        Dictionary<string, string> from = Reverse[(int)_rendered];
        int to = (int)Current;

        Walk(root, from, to);
        _rendered = Current;
    }

    private static void Walk(Node node, Dictionary<string, string> from, int to)
    {
        switch (node)
        {
            case Label l:
                if (from.TryGetValue(l.Text, out string k1)) l.Text = ByKey[k1][to];
                break;
            case Button b:
                if (from.TryGetValue(b.Text, out string k2)) b.Text = ByKey[k2][to];
                break;
        }

        foreach (Node child in node.GetChildren())
            Walk(child, from, to);
    }
}
