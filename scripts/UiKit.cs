using Godot;

/// <summary>
/// 素材与样式工具箱：Sprout Lands 素材图集切片、像素字体（中文自动回退默认字体）、
/// 木质九宫格样式。所有区域坐标均由 PIL 对素材表实测得出。
/// </summary>
public static class UiKit
{
    // —— 纹理 ——
    public static readonly Texture2D Buttons = Load("res://assets/sprout/buttons.png");
    public static readonly Texture2D Emoji = Load("res://assets/sprout/emoji.png");
    public static readonly Texture2D PanelTex = Load("res://assets/sprout/panel.png");
    public static readonly Texture2D Playbar = Load("res://assets/sprout/playbar.png");
    public static readonly Texture2D Settings = Load("res://assets/sprout/settings.png");

    private static Texture2D Load(string path) => GD.Load<Texture2D>(path);

    // —— 字体 ——
    // 主字体：字由文艺黑（本机字体，现代利落），缺字回退默认字体
    private static FontFile _uiFont;
    public static FontFile UiFont => _uiFont ??= MakeUiFont();

    private static FontFile MakeUiFont()
    {
        var f = GD.Load<FontFile>("res://assets/fonts/wenyi_hei.ttf");

        // 中文主字体没有假名/谚文/泰文，按顺序挂上 Noto 子集（只保留界面用到的字符，共约 160 KB）。
        // 子集由 tools/make_font_subsets.py 生成，字重统一 Bold，和中文主字体观感一致。
        var fallbacks = new Godot.Collections.Array<Font>();
        foreach (string path in new[]
        {
            "res://assets/fonts/noto_jp_subset.otf",
            "res://assets/fonts/noto_kr_subset.otf",
            "res://assets/fonts/noto_th_subset.ttf",
        })
        {
            var extra = GD.Load<FontFile>(path);
            if (extra != null) fallbacks.Add(extra);
        }
        fallbacks.Add(ThemeDB.FallbackFont);

        f.Fallbacks = fallbacks;
        return f;
    }

    // 像素字体：拉丁装饰备用
    private static FontFile _pixel;
    public static FontFile PixelFont => _pixel ??= MakePixelFont();

    // 繁体专用：文艺黑缺 44 个字，繁体的字几乎必然掉到系统细体，
    // 所以繁体整门语言都改用 Noto Sans TC 子集，避免一句话里两种字重。
    private static Font _hant;
    public static Font HantFont => _hant ??= MakeHantFont();

    private static Font MakeHantFont()
    {
        // 首选圆体「粉圆」，只有 Regular，用 FontVariation 加粗贴近主字体的笔画厚度；
        // 缺失时退回 Noto TC（方一些，但字重本来就对）。
        var huninn = GD.Load<FontFile>("res://assets/fonts/huninn_tc_subset.ttf");
        FontFile f = huninn ?? GD.Load<FontFile>("res://assets/fonts/noto_tc_subset.otf");
        if (f == null) return UiFont;      // 未导入/缺文件时退回主字体，别让 UI 崩掉

        var fb = new Godot.Collections.Array<Font>();
        var jp = GD.Load<FontFile>("res://assets/fonts/noto_jp_subset.otf");
        if (jp != null) fb.Add(jp);      // 子集外的字先用日文字库兜一层
        fb.Add(ThemeDB.FallbackFont);
        f.Fallbacks = fb;

        // 粉圆只有 Regular，模拟加粗一档，避免繁体比简体细一圈
        if (huninn == null) return f;
        return new FontVariation { BaseFont = f, VariationEmbolden = 0.6f, Fallbacks = fb };
    }

    /// <summary>按语言取界面字体：繁体走独立的 TC 字库，其余用中文主字体。</summary>
    public static Font UiFontFor(Loc.Id lang) =>
        lang == Loc.Id.ZhHant ? HantFont : UiFont;

    private static FontFile MakePixelFont()
    {
        var f = GD.Load<FontFile>("res://assets/sprout/pixel_font.ttf");
        f.Fallbacks = new Godot.Collections.Array<Font> { ThemeDB.FallbackFont };
        return f;
    }

    // —— 图集切片 ——
    public static AtlasTexture At(Texture2D tex, int x, int y, int w, int h) =>
        new() { Atlas = tex, Region = new Rect2(x, y, w, h) };

    // emoji 表：32px 单元格网格（5 列 × 19 行），每个 sprite 占一格；
    // 区域由 PIL 连通域实测确认（16px 网格会把笑脸切成四分之一）
    public static readonly AtlasTexture FaceGreen = At(Emoji, 0, 0, 32, 32);      // 微笑绿脸 · 蛇头吉祥物
    public static readonly AtlasTexture FaceGreen2 = At(Emoji, 32, 0, 32, 32);
    public static readonly AtlasTexture FaceYellow = At(Emoji, 64, 0, 32, 32);    // 黄脸 · 标准
    public static readonly AtlasTexture FaceOrange = At(Emoji, 96, 0, 32, 32);    // 橙脸 · 地狱
    public static readonly AtlasTexture FacePink = At(Emoji, 128, 0, 32, 32);
    public static readonly AtlasTexture FaceLove = At(Emoji, 0, 32, 32, 32);      // 爱心眼 · 胜利
    public static readonly AtlasTexture FaceDead = At(Emoji, 64, 32, 32, 32);
    public static readonly AtlasTexture FaceCry = At(Emoji, 96, 32, 32, 32);      // 哭脸 · 失败
    public static readonly AtlasTexture FaceSadBlue = At(Emoji, 128, 32, 32, 32);
    public static readonly AtlasTexture Check = At(Emoji, 0, 224, 32, 32);
    public static readonly AtlasTexture Cross = At(Emoji, 32, 224, 32, 32);
    public static readonly AtlasTexture Excl = At(Emoji, 64, 224, 32, 32);
    public static readonly AtlasTexture Question = At(Emoji, 96, 224, 32, 32);
    public static readonly AtlasTexture Zzz = At(Emoji, 128, 224, 32, 32);
    public static readonly AtlasTexture Note = At(Emoji, 0, 256, 32, 32);
    public static readonly AtlasTexture Coin = At(Emoji, 32, 256, 32, 32);
    public static readonly AtlasTexture HeartSmall = At(Emoji, 64, 256, 32, 32);
    public static readonly AtlasTexture Heart = At(Emoji, 96, 256, 32, 32);
    public static readonly AtlasTexture Star = At(Emoji, 128, 256, 32, 32);
    public static readonly AtlasTexture ThumbUp = At(Emoji, 0, 288, 32, 32);
    public static readonly AtlasTexture Sprout = At(Emoji, 0, 384, 32, 32);
    public static readonly AtlasTexture HeartFlower = At(Emoji, 32, 384, 32, 32);
    public static readonly AtlasTexture Clover = At(Emoji, 64, 384, 32, 32);
    public static readonly AtlasTexture Mushroom = At(Emoji, 96, 384, 32, 32);
    public static readonly AtlasTexture Apple = At(Emoji, 0, 416, 32, 32);
    public static readonly AtlasTexture Wheat = At(Emoji, 32, 416, 32, 32);
    public static readonly AtlasTexture Tomato = At(Emoji, 64, 416, 32, 32);
    public static readonly AtlasTexture Corn = At(Emoji, 96, 416, 32, 32);
    public static readonly AtlasTexture Carrot = At(Emoji, 128, 416, 32, 32);
    public static readonly AtlasTexture Tree = At(Emoji, 0, 448, 32, 32);
    public static readonly AtlasTexture Bush = At(Emoji, 32, 448, 32, 32);
    public static readonly AtlasTexture Leaf = At(Emoji, 64, 448, 32, 32);
    public static readonly AtlasTexture Rock = At(Emoji, 128, 448, 32, 32);
    public static readonly AtlasTexture Sun = At(Emoji, 0, 576, 32, 32);
    public static readonly AtlasTexture Cloud = At(Emoji, 32, 576, 32, 32);
    public static readonly AtlasTexture RainCloud = At(Emoji, 64, 576, 32, 32);
    public static readonly AtlasTexture Sunset = At(Emoji, 96, 576, 32, 32);
    public static readonly AtlasTexture Moon = At(Emoji, 128, 576, 32, 32);

    // 方形木按钮表：2 列（正常/按下）× 4 行（灰白/奶油/棕黄/深棕），含 1px 间距
    public static readonly AtlasTexture BtnWhite = At(Buttons, 11, 11, 26, 28);
    public static readonly AtlasTexture BtnWhitePress = At(Buttons, 59, 11, 26, 28);
    public static readonly AtlasTexture BtnCream = At(Buttons, 11, 59, 26, 28);
    public static readonly AtlasTexture BtnCreamPress = At(Buttons, 59, 59, 26, 28);
    public static readonly AtlasTexture BtnTan = At(Buttons, 11, 107, 26, 28);
    public static readonly AtlasTexture BtnTanPress = At(Buttons, 59, 107, 26, 28);
    public static readonly AtlasTexture BtnBrown = At(Buttons, 11, 155, 26, 28);
    public static readonly AtlasTexture BtnBrownPress = At(Buttons, 59, 155, 26, 28);

    // 大横条按钮表：2×2 网格（列=正常/按下，行=无字/带 PLAY），每格 90x27，由 PIL 实测
    public static readonly AtlasTexture BarPlain = At(Playbar, 3, 2, 90, 27);
    public static readonly AtlasTexture BarPlainPress = At(Playbar, 99, 2, 90, 27);
    public static readonly AtlasTexture BarPlay = At(Playbar, 3, 34, 90, 27);
    public static readonly AtlasTexture BarPlayPress = At(Playbar, 99, 34, 90, 27);

    // 设置表：旋钮 / 开关槽
    public static readonly AtlasTexture KnobCream = At(Settings, 7, 86, 18, 19);
    public static readonly AtlasTexture KnobGreen = At(Settings, 71, 86, 18, 19);
    public static readonly AtlasTexture TrackBrown = At(Settings, 2, 121, 28, 14);
    public static readonly AtlasTexture TrackGreen = At(Settings, 34, 121, 28, 14);

    // —— 木质九宫格样式 ——
    public static StyleBoxTexture WoodPanel(int margin = 12)
    {
        var sb = new StyleBoxTexture { Texture = PanelTex };
        sb.TextureMarginLeft = margin; sb.TextureMarginTop = margin;
        sb.TextureMarginRight = margin; sb.TextureMarginBottom = margin;
        sb.ContentMarginLeft = margin + 6; sb.ContentMarginTop = margin + 4;
        sb.ContentMarginRight = margin + 6; sb.ContentMarginBottom = margin + 4;
        return sb;
    }

    /// <summary>木条按钮底（横条九宫格）。</summary>
    public static StyleBoxTexture WoodBar(AtlasTexture region = null)
    {
        var sb = new StyleBoxTexture { Texture = Playbar, RegionRect = (region ?? BarPlain).Region };
        sb.TextureMarginLeft = 12; sb.TextureMarginRight = 12;
        sb.TextureMarginTop = 9; sb.TextureMarginBottom = 9;
        sb.ContentMarginLeft = 12; sb.ContentMarginRight = 12;
        sb.ContentMarginTop = 6; sb.ContentMarginBottom = 6;
        return sb;
    }

    /// <summary>方形木按钮底（26x28 图块拉伸，正常/按下两态）。</summary>
    public static StyleBoxTexture WoodSq(AtlasTexture normal)
    {
        var sb = new StyleBoxTexture { Texture = Buttons, RegionRect = normal.Region };
        sb.TextureMarginLeft = 9; sb.TextureMarginRight = 9;
        sb.TextureMarginTop = 9; sb.TextureMarginBottom = 11;
        sb.ContentMarginLeft = 6; sb.ContentMarginRight = 6;
        sb.ContentMarginTop = 4; sb.ContentMarginBottom = 4;
        return sb;
    }
}
