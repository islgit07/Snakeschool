using Godot;
using System.Collections.Generic;

/// <summary>
/// 全部界面：主菜单（logo / 难度 / 开始 / 退出）、顶部状态栏（液态体重条）、
/// 移动端虚拟按键（圆形方向键 + 可拖动摇杆 + 催吐）、暂停与结算弹窗。
/// 视觉基于 Sprout Lands UI Pack（木质面板 / emoji 图标），
/// 主字体为站酷庆科黄油体，动画节奏借鉴 MrEliptik/godot_ui_components（MIT）。
/// </summary>
public partial class UiLayer : CanvasLayer
{
    public Main Game;

    private Control _root;
    private Control _hud;
    private Label _weightLabel;
    private LiquidBar _weightBar;
    private Label _appleLabel;
    private Button _pauseBtn;
    private Button _speedBtn;

    private Control _pad;
    private Button _spitBtn;
    private Label _spitState;
    private JoystickPanel _joy;
    private Panel _joyKnob;

    private Control _menu;
    private Button[] _diffBtns;
    private TextureRect[] _diffBadges;
    private int _selDiff = 1;
    private TextureRect _mascot;
    private Label _title;
    private Button _startBtn, _exitBtn;
    private Button _langBtn;
    private Panel _hillBack, _hillFront;
    private Tween _intro;

    private Control _introPanel;
    private Panel _introBox;
    private Label _introLine1, _introLine2;
    private Button _goBtn;
    private DifficultyPreset _pendingDiff;

    private Control _langPanel;
    private TextureRect[] _langMarks;
    private Label _pauseTitle;

    private Control _pausePanel;
    private Control _overPanel;
    private Panel _overBox;
    private Button _overAgain, _overMenu;
    private TextureRect _overFace;
    private Label _overTitle, _overBadge, _overBody;

    private ColorRect _dangerFx;
    private ShaderMaterial _dangerMat;
    private float _fxTime;

    private Panel _achToast;
    private Label _achName;
    private readonly Queue<string> _achQueue = new();
    private bool _achBusy;

    public override void _Ready()
    {
        Loc.Init();

        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        BuildHud();
        BuildPad();
        BuildMenu();
        BuildIntro();
        BuildPause();
        BuildOver();
        BuildDangerFx();
        BuildAchievements();
        BuildLangPanel();

        SelectDiff(_selDiff);

        // 建完后统一翻译一次；之后切换语言只重走这一遍 + 刷新动态文本
        Loc.Changed += ApplyLanguage;
        Loc.TranslateTree(_root);
        ApplyUiFont();
        ApplyLanguageStyles();
    }

    /// <summary>语言切换：静态文案走控件树翻译，动态文案单独刷新。</summary>
    private void ApplyLanguage()
    {
        Loc.TranslateTree(_root);
        ApplyUiFont();
        ApplyLanguageStyles();
        RefreshLangMarks();
        if (Game != null)
        {
            UpdateHud();      // 体重 / 催吐状态
            UpdateSpeed();    // ×1 / ×2 与配色
        }
    }

    /// <summary>繁体的字文艺黑缺四成，整门语言换成 TC 字库，避免同句两种字重。</summary>
    private void ApplyUiFont()
    {
        Font font = UiKit.UiFontFor(Loc.Current);
        SetFontRecursive(_root, font);
        if (_title != null && _title.LabelSettings != null) _title.LabelSettings.Font = font;
    }

    private static void SetFontRecursive(Node node, Font font)
    {
        switch (node)
        {
            case Label l: l.AddThemeFontOverride("font", font); break;
            case Button b: b.AddThemeFontOverride("font", font); break;
        }
        foreach (Node child in node.GetChildren())
            SetFontRecursive(child, font);
    }

    /// <summary>
    /// 各语言单词长度差得远（"WYMIOT" vs "催吐"），圆按钮和标题按实测宽度自动收字号。
    /// </summary>
    private void ApplyLanguageStyles()
    {
        FitFont(_spitBtn, 168f, 46);
        FitFont(_pauseBtn, 48f, 30);
        FitFont(_overTitle, 460f, 48);
        FitFont(_achName, 424f, 30);
        FitFont(_pauseTitle, 430f, 48);
        FitFont(_introLine1, 462f, 34);
        FitFont(_introLine2, 462f, 27);
        FitFont(_goBtn, 150f, 34);
        _langBtn.Text = Loc.ShortName;
        FitFont(_langBtn, 84f, 26);
    }

    /// <summary>把控件字号一路收到文本宽度装得下为止。</summary>
    private static void FitFont(Control c, float maxWidth, int baseSize)
    {
        if (c == null) return;
        string text = c switch
        {
            Button b => b.Text,
            Label l => l.Text,
            _ => "",
        };
        if (string.IsNullOrEmpty(text)) return;

        Font font = c.GetThemeFont("font") ?? UiKit.UiFont;
        int size = baseSize;
        while (size > 10 &&
               font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X > maxWidth)
            size -= 2;
        c.AddThemeFontSizeOverride("font_size", size);
    }

    /// <summary>成就弹出条（灵动岛式：从屏幕顶部滑入）。</summary>
    private void BuildAchievements()
    {
        _achToast = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        _achToast.Size = new Vector2(560, 92);
        var sb = new StyleBoxFlat { BgColor = new Color(0.11f, 0.09f, 0.07f, 0.94f) };
        sb.SetCornerRadiusAll(46);
        sb.BorderColor = Palette.Gold;
        sb.SetBorderWidthAll(2);
        sb.ShadowColor = new Color(0, 0, 0, 0.4f);
        sb.ShadowSize = 6;
        sb.ShadowOffset = new Vector2(0, 4);
        _achToast.AddThemeStyleboxOverride("panel", sb);
        _root.AddChild(_achToast); // 在警报暗角之上

        var medal = CircleStyle(60, new Color("8a6d1f"));
        medal.SetCornerRadiusAll(30);
        var medalPanel = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        medalPanel.Position = new Vector2(18, 16);
        medalPanel.Size = new Vector2(60, 60);
        medalPanel.AddThemeStyleboxOverride("panel", medal);
        _achToast.AddChild(medalPanel);
        medalPanel.AddChild(Sprite(UiKit.Star, 10, 10, 40));

        var caption = TextLabel("成就解锁", 18, Palette.Gold);
        caption.Position = new Vector2(96, 16);
        caption.Size = new Vector2(440, 26);
        _achToast.AddChild(caption);

        _achName = TextLabel("", 30, Palette.Text);
        _achName.Position = new Vector2(96, 44);
        _achName.Size = new Vector2(440, 40);
        _achToast.AddChild(_achName);
    }

    /// <summary>成就解锁请求：入队后依次以灵动岛方式弹出。</summary>
    public void QueueAchievement(string id)
    {
        _achQueue.Enqueue(id);
        ShowNextAchievement();
    }

    private async void ShowNextAchievement()
    {
        if (_achBusy || _achQueue.Count == 0) return;
        _achBusy = true;
        _achName.Text = Loc.T(Achievements.NameOf(_achQueue.Dequeue()));

        _achToast.Position = new Vector2(80, -110);
        _achToast.Visible = true;
        AudioManager.Instance?.PlayMinor(AudioBank.Win, 1.35f);

        var tIn = CreateTween();
        tIn.TweenProperty(_achToast, "position:y", 14f, 0.5f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        await ToSignal(tIn, Tween.SignalName.Finished);
        await ToSignal(GetTree().CreateTimer(2.3), "timeout");

        var tOut = CreateTween();
        tOut.TweenProperty(_achToast, "position:y", -110f, 0.4f)
            .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
        await ToSignal(tOut, Tween.SignalName.Finished);
        _achToast.Visible = false;

        _achBusy = false;
        ShowNextAchievement();
    }

    /// <summary>肥胖警报红色暗角（全屏最顶层，鼠标穿透）。</summary>
    private void BuildDangerFx()
    {
        _dangerMat = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shaders/vignette.gdshader") };
        _dangerMat.SetShaderParameter("intensity", 0f);
        _dangerMat.SetShaderParameter("pulse", 0f);
        _dangerMat.SetShaderParameter("danger_color", new Color(0.85f, 0.16f, 0.1f));

        _dangerFx = new ColorRect
        {
            Color = Colors.White, // 颜色由 shader 输出
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
            Material = _dangerMat,
        };
        _dangerFx.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_dangerFx); // 最后添加 → 盖在所有界面之上
    }

    public override void _Process(double delta)
    {
        if (Game == null || _dangerFx == null) return;
        _fxTime += (float)delta;

        float alarm = Game.State == GameState.Menu ? 0f : Game.FatAlarm; // 两倍体重起警
        bool active = alarm > 0f && Game.State is GameState.Playing or GameState.Paused or GameState.Lost;
        _dangerFx.Visible = active;
        if (!active) return;

        // 心跳脉冲：正弦高次幂做出"咚-咚"节奏
        float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_fxTime * 5.2f)), 6f);
        _dangerMat.SetShaderParameter("intensity", alarm);
        _dangerMat.SetShaderParameter("pulse", pulse);
    }

    // ———————————————— 小工厂 ————————————————

    private static Label TextLabel(string text, int size, Color col)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeFontOverride("font", UiKit.UiFont);
        l.AddThemeColorOverride("font_color", col);
        return l;
    }

    private static TextureRect Sprite(AtlasTexture tex, float x, float y, float size)
    {
        return new TextureRect
        {
            Texture = tex,
            Position = new Vector2(x, y),
            Size = new Vector2(size, size),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
    }

    /// <summary>木条按钮（大横条），带按压 Q 弹。</summary>
    private Button BarButton(string text, int fontSize, float x, float y, float w, float h)
    {
        var b = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            Position = new Vector2(x, y),
            Size = new Vector2(w, h),
            PivotOffset = new Vector2(w / 2f, h / 2f),
        };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        b.AddThemeFontOverride("font", UiKit.UiFont);
        b.AddThemeColorOverride("font_color", Palette.Text);
        b.AddThemeColorOverride("font_hover_color", Palette.Text);
        b.AddThemeColorOverride("font_pressed_color", Palette.Ink);
        b.AddThemeStyleboxOverride("normal", UiKit.WoodBar());
        b.AddThemeStyleboxOverride("hover", Darken(UiKit.WoodBar(), 0.94f));
        b.AddThemeStyleboxOverride("pressed", UiKit.WoodBar(UiKit.BarPlainPress));
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        return b;
    }

    /// <summary>圆形按钮样式（CircleButton 与倍速按钮刷新共用）。</summary>
    private static StyleBoxFlat CircleStyle(float d, Color bg)
    {
        var sb = new StyleBoxFlat { BgColor = bg };
        sb.SetCornerRadiusAll((int)(d / 2f));
        sb.BorderColor = Palette.Ink;
        sb.SetBorderWidthAll(3);
        sb.ShadowColor = new Color(0.42f, 0.31f, 0.18f, 0.35f);
        sb.ShadowSize = 4;
        sb.ShadowOffset = new Vector2(0, 4);
        return sb;
    }

    /// <summary>圆形按钮：大圆角 + 深棕描边 + 底部阴影。</summary>
    private Button CircleButton(string text, int fontSize, float d, Color bg, Color fg)
    {
        var b = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            Size = new Vector2(d, d),
            PivotOffset = new Vector2(d / 2f, d / 2f),
        };
        StyleBoxFlat Make(Color c) => CircleStyle(d, c);
        b.AddThemeFontSizeOverride("font_size", fontSize);
        b.AddThemeFontOverride("font", UiKit.UiFont);
        b.AddThemeColorOverride("font_color", fg);
        b.AddThemeColorOverride("font_hover_color", fg);
        b.AddThemeColorOverride("font_pressed_color", fg);
        b.AddThemeStyleboxOverride("normal", Make(bg));
        b.AddThemeStyleboxOverride("hover", Make(bg.Lightened(0.07f)));
        b.AddThemeStyleboxOverride("pressed", Make(bg.Darkened(0.16f)));
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        return b;
    }

    /// <summary>方形木按钮（26x28 图块拉伸两态）。</summary>
    private Button SquareButton(string text, int fontSize, AtlasTexture normal, AtlasTexture pressed)
    {
        var b = new Button
        {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
        };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        b.AddThemeFontOverride("font", UiKit.UiFont);
        b.AddThemeColorOverride("font_color", Palette.Text);
        b.AddThemeColorOverride("font_hover_color", Palette.Text);
        b.AddThemeColorOverride("font_pressed_color", Palette.Ink);
        b.AddThemeStyleboxOverride("normal", UiKit.WoodSq(normal));
        b.AddThemeStyleboxOverride("hover", Darken(UiKit.WoodSq(normal), 0.94f));
        b.AddThemeStyleboxOverride("pressed", UiKit.WoodSq(pressed));
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        return b;
    }

    private static StyleBoxTexture Darken(StyleBoxTexture src, float k)
    {
        var sb = (StyleBoxTexture)src.Duplicate();
        sb.ModulateColor = new Color(k, k, k);
        return sb;
    }

    private void Wire(Button b, System.Action action)
    {
        b.Pressed += () =>
        {
            AudioManager.Instance?.PlayMinor(AudioBank.Click);
            Squish(b);
            action();
        };
    }

    /// <summary>按压 Q 弹（弹性回弹节奏）。</summary>
    private void Squish(Control c)
    {
        c.PivotOffset = c.Size / 2f;
        var t = CreateTween();
        t.TweenProperty(c, "scale", new Vector2(0.92f, 0.92f), 0.06f).SetEase(Tween.EaseType.In);
        t.TweenProperty(c, "scale", Vector2.One, 0.22f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Elastic);
    }

    // ———————————————— 顶部状态栏 ————————————————

    private void BuildHud()
    {
        _hud = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _hud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        _hud.OffsetBottom = 96;
        _hud.Visible = false;
        _root.AddChild(_hud);

        var bg = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        bg.AddThemeStyleboxOverride("panel", UiKit.WoodPanel(12));
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _hud.AddChild(bg);

        _weightLabel = TextLabel("体重 18 节 · 肥胖", 26, Palette.Text);
        _weightLabel.Position = new Vector2(26, 8);
        _hud.AddChild(_weightLabel);

        _weightBar = new LiquidBar();
        _weightBar.Position = new Vector2(26, 52);
        _weightBar.Size = new Vector2(360, 28);
        _hud.AddChild(_weightBar);

        _hud.AddChild(Sprite(UiKit.Apple, 404, 14, 30));
        _appleLabel = TextLabel("0", 28, Palette.Text);
        _appleLabel.Position = new Vector2(442, 12);
        _hud.AddChild(_appleLabel);

        _pauseBtn = CircleButton("暂停", 22, 56, Palette.Cream, Palette.Text);
        _pauseBtn.Position = new Vector2(544, 20);
        Wire(_pauseBtn, () => Game.TogglePause());
        _hud.AddChild(_pauseBtn);

        // 右上角：二倍速开关（激活时变绿）
        _speedBtn = CircleButton("×1", 22, 56, Palette.Cream, Palette.Text);
        _speedBtn.Position = new Vector2(612, 20);
        Wire(_speedBtn, () => Game.ToggleSpeed());
        _hud.AddChild(_speedBtn);
    }

    // ———————————————— 底部虚拟按键（手机适配核心） ————————————————

    private void BuildPad()
    {
        _pad = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _pad.AnchorLeft = 0; _pad.AnchorRight = 1;
        _pad.AnchorTop = 1; _pad.AnchorBottom = 1;
        _pad.OffsetTop = -460; _pad.OffsetBottom = 0;
        _pad.Visible = false;
        _root.AddChild(_pad);

        var padBg = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        padBg.AddThemeStyleboxOverride("panel", UiKit.WoodPanel(12));
        padBg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pad.AddChild(padBg);

        // 中央摇杆：按住圆盘拖动即可转向（类王者左手轮盘）
        _joy = new JoystickPanel { Position = new Vector2(108, 206), Size = new Vector2(124, 124) };
        _joy.AddThemeStyleboxOverride("panel", CircleStyle(124, Palette.Cream));
        _joy.OnDir = d => Game.SetDir(d);
        _joy.OnOffset = off => _joyKnob.Position = new Vector2(30, 30) + off * 30f;
        _joy.OnRelease = () =>
        {
            var t = CreateTween();
            t.TweenProperty(_joyKnob, "position", new Vector2(30, 30), 0.3f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        };
        _pad.AddChild(_joy);

        _joyKnob = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        _joyKnob.Position = new Vector2(30, 30);
        _joyKnob.Size = new Vector2(64, 64);
        var knobSb = CircleStyle(64, Palette.Green);
        knobSb.ShadowSize = 0;
        _joyKnob.AddThemeStyleboxOverride("panel", knobSb);
        _joy.AddChild(_joyKnob);
        _joyKnob.AddChild(Sprite(UiKit.FaceGreen, 12, 12, 40));

        // 圆形方向键
        Vector2I[] dirs = { Vector2I.Up, Vector2I.Left, Vector2I.Right, Vector2I.Down };
        Vector2[] pos = { new(118, 116), new(14, 216), new(220, 216), new(118, 316) };
        for (int i = 0; i < dirs.Length; i++)
        {
            Vector2I d = dirs[i];
            var b = CircleButton("", 20, 104, Palette.Cream, Palette.Text);
            b.Position = pos[i];
            var arrow = new ArrowGlyph { MouseFilter = Control.MouseFilterEnum.Ignore };
            arrow.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            arrow.Painter = c => PaintArrow(c, d);
            b.AddChild(arrow);
            Wire(b, () => Game.SetDir(d));
            _pad.AddChild(b);
        }

        // 圆形催吐大按钮
        _spitBtn = CircleButton("催吐", 46, 182, new Color("e7c48e"), Palette.Text);
        _spitBtn.Position = new Vector2(459, 132);
        Wire(_spitBtn, () => Game.TrySpit());
        _pad.AddChild(_spitBtn);

        _spitState = TextLabel("就绪 · 每步一次", 18, Palette.LeafDark);
        _spitState.Position = new Vector2(439, 326);
        _spitState.Size = new Vector2(222, 30);
        _spitState.HorizontalAlignment = HorizontalAlignment.Center;
        _pad.AddChild(_spitState);
    }

    // ———————————————— 主菜单：logo + 难度 + 开始 + 退出 ————————————————

    private void BuildMenu()
    {
        _menu = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _menu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _menu.Visible = false;
        _root.AddChild(_menu);

        // 奶油渐变背景
        var bg = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient { Colors = new[] { Palette.BgTop, Palette.BgBottom }, Offsets = new[] { 0f, 1f } },
                FillFrom = new Vector2(0, 0),
                FillTo = new Vector2(0, 1),
                Width = 64,
                Height = 256,
            },
            MouseFilter = Control.MouseFilterEnum.Ignore,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        bg.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _menu.AddChild(bg);

        // logo 背后的柔光圆（呼吸：缓慢缩放 + 明暗脉动）
        var halo = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        halo.Position = new Vector2(150, -30);
        halo.Size = new Vector2(420, 420);
        halo.PivotOffset = new Vector2(210, 210);
        var haloSb = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.38f) };
        haloSb.SetCornerRadiusAll(210);
        _menu.AddChild(halo);
        halo.AddThemeStyleboxOverride("panel", haloSb);
        var haloTw = CreateTween().SetLoops();
        haloTw.TweenProperty(halo, "scale", new Vector2(1.06f, 1.06f), 2.2f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        haloTw.TweenProperty(halo, "scale", Vector2.One, 2.2f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        var haloTw2 = CreateTween().SetLoops();
        haloTw2.TweenProperty(halo, "modulate:a", 0.82f, 2.2f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        haloTw2.TweenProperty(halo, "modulate:a", 1f, 2.2f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

        // 底部两道草坡（曲率构图），缓慢上下浮动
        _hillBack = AddHill(-140, 1010, 1000, 400, 220, new Color("e2d5a4"));
        _hillFront = AddHill(-80, 1120, 880, 300, 200, new Color("fff1cf"));
        FloatLoop(_hillBack, 14f, 2.8f);
        FloatLoop(_hillFront, 10f, 2.2f);

        // 游动线条：几条蛇形波浪在背景里"跑"，呼应贪吃蛇主题
        var trails = new SnakeTrail { MouseFilter = Control.MouseFilterEnum.Ignore };
        trails.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _menu.AddChild(trails);

        // 游戏 logo：吉祥物 + 标题
        _mascot = Sprite(UiKit.FaceGreen, 300, 96, 120);
        _mascot.PivotOffset = new Vector2(60, 60);
        _menu.AddChild(_mascot);
        var mascTw = CreateTween().SetLoops();
        mascTw.TweenProperty(_mascot, "position:y", 84f, 1.1f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        mascTw.TweenProperty(_mascot, "position:y", 96f, 1.1f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

        _title = new Label
        {
            Text = "贪吃蛇减肥",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings
            {
                Font = UiKit.UiFont,
                FontSize = 96,
                FontColor = Palette.Text,
                OutlineSize = 10,
                OutlineColor = Palette.Cream,
                ShadowColor = new Color(0.45f, 0.34f, 0.2f, 0.3f),
                ShadowOffset = new Vector2(0, 6),
            },
        };
        _title.AnchorRight = 1;
        _title.OffsetTop = 236;
        _title.OffsetBottom = 372;
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _title.PivotOffset = new Vector2(360, 68);
        _menu.AddChild(_title);

        // 难度选择（三块木牌 + 表情脸）
        string[] names = { "轻松", "标准", "地狱" };
        AtlasTexture[] faces = { UiKit.FaceGreen, UiKit.FaceYellow, UiKit.FaceOrange };
        AtlasTexture[][] rows =
        {
            new[] { UiKit.BtnCream, UiKit.BtnCreamPress },
            new[] { UiKit.BtnTan, UiKit.BtnTanPress },
            new[] { UiKit.BtnBrown, UiKit.BtnBrownPress },
        };
        _diffBtns = new Button[names.Length];
        _diffBadges = new TextureRect[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            var b = SquareButton("", 28, rows[i][0], rows[i][1]);
            b.Position = new Vector2(40 + i * 220, 460);
            b.Size = new Vector2(200, 110);
            b.PivotOffset = new Vector2(100, 55);
            var face = Sprite(faces[i], 72, 12, 56);
            b.AddChild(face);
            var lb = TextLabel(names[i], 28, Palette.Text);
            lb.Position = new Vector2(0, 70);
            lb.Size = new Vector2(200, 36);
            lb.HorizontalAlignment = HorizontalAlignment.Center;
            b.AddChild(lb);
            var badge = Sprite(UiKit.Check, 158, 6, 32);
            badge.Visible = false;
            b.AddChild(badge);
            _diffBadges[i] = badge;
            Wire(b, () => SelectDiff(idx));
            _diffBtns[i] = b;
            _menu.AddChild(b);
        }

        // 开始 / 退出（统一尺寸与字号）
        _startBtn = BarButton("开始游戏", 34, 160, 640, 400, 96);
        Wire(_startBtn, () => TryStart(GameConfig.Difficulties[_selDiff]));
        _menu.AddChild(_startBtn);

        _exitBtn = BarButton("退出游戏", 34, 160, 760, 400, 96);
        Wire(_exitBtn, () => GetTree().Quit());
        _menu.AddChild(_exitBtn);

        // 底部版本号（读工程设置，不再手写死）
        var version = TextLabel($"v{ProjectSettings.GetSetting("application/config/version", "0.0.0").AsString()}", 16, new Color(0.52f, 0.44f, 0.32f, 0.75f));
        version.AnchorRight = 1;
        version.AnchorTop = 1;
        version.AnchorBottom = 1;
        version.OffsetTop = -36;
        version.OffsetBottom = -10;
        version.HorizontalAlignment = HorizontalAlignment.Center;
        _menu.AddChild(version);

        // 右上角语言选择：点开列出 15 种语言
        _langBtn = SquareButton("EN", 26, UiKit.BtnCream, UiKit.BtnCreamPress);
        _langBtn.Position = new Vector2(596, 30);
        _langBtn.Size = new Vector2(96, 58);
        _langBtn.PivotOffset = new Vector2(48, 29);
        Wire(_langBtn, OpenLangPanel);
        _menu.AddChild(_langBtn);
    }

    /// <summary>首次点「开始游戏」：先展示减肥宣言，确认后再真正开局。</summary>
    private void TryStart(DifficultyPreset diff)
    {
        if (!Game.Audio.IntroSeen)
        {
            _pendingDiff = diff;
            Game.Audio.MarkIntroSeen(); // 弹过即算看过，之后直接开局
            ShowIntro();
            return;
        }
        Game.StartWithTransition(diff);
    }

    private void BuildIntro()
    {
        _introPanel = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _introPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _introPanel.Visible = false;
        _root.AddChild(_introPanel);

        var dim = new ColorRect { Color = new Color(0.3f, 0.22f, 0.12f, 0.45f) };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _introPanel.AddChild(dim);

        _introBox = new Panel();
        _introBox.Position = new Vector2(110, 420);
        _introBox.Size = new Vector2(500, 430);
        _introBox.AddThemeStyleboxOverride("panel", UiKit.WoodPanel(12));
        _introPanel.AddChild(_introBox);

        // 吉祥物趴在牌子上沿
        var mascot = Sprite(UiKit.FaceGreen, 212, -38, 76);
        _introBox.AddChild(mascot);

        var line1 = TextLabel("贪吃蛇决定要减肥了", 34, Palette.Text);
        line1.Position = new Vector2(0, 84);
        line1.Size = new Vector2(500, 52);
        line1.HorizontalAlignment = HorizontalAlignment.Center;
        _introBox.AddChild(line1);
        _introLine1 = line1;

        var line2 = TextLabel("不要再贪吃了哦", 27, Palette.SubText);
        line2.Position = new Vector2(0, 152);
        line2.Size = new Vector2(500, 44);
        line2.HorizontalAlignment = HorizontalAlignment.Center;
        _introBox.AddChild(line2);
        _introLine2 = line2;

        var go = BarButton("出发", 34, 168, 276, 320, 88);
        Wire(go, () =>
        {
            _introPanel.Visible = false;
            Game.StartWithTransition(_pendingDiff);
        });
        _introBox.AddChild(go);
        _goBtn = go;
    }

    // ———————————————— 语言选择面板 ————————————————

    private void BuildLangPanel()
    {
        _langPanel = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _langPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _langPanel.Visible = false;
        _root.AddChild(_langPanel);

        var dim = new ColorRect { Color = new Color(0.3f, 0.22f, 0.12f, 0.55f) };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _langPanel.AddChild(dim);

        var box = new Panel { Position = new Vector2(56, 150), Size = new Vector2(608, 980) };
        box.AddThemeStyleboxOverride("panel", UiKit.WoodPanel(12));
        _langPanel.AddChild(box);

        var title = TextLabel("语言", 40, Palette.Text);
        title.Position = new Vector2(0, 24);
        title.Size = new Vector2(608, 58);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);

        _langMarks = new TextureRect[Loc.Count];
        for (int i = 0; i < Loc.Count; i++)
        {
            int idx = i;
            int col = i % 2, row = i / 2;
            var b = SquareButton(Loc.NativeName((Loc.Id)i), 24, UiKit.BtnCream, UiKit.BtnCreamPress);
            b.Position = new Vector2(16 + col * 292, 96 + row * 92);
            b.Size = new Vector2(276, 78);
            b.PivotOffset = new Vector2(138, 39);
            Wire(b, () =>
            {
                Loc.Set((Loc.Id)idx);
                CloseLangPanel();
            });
            FitFont(b, 236f, 24);            // 长语言名（Português (Brasil) 等）自动收字号
            var mark = Sprite(UiKit.Check, 240, 8, 28);
            mark.Visible = false;
            b.AddChild(mark);
            _langMarks[i] = mark;
            box.AddChild(b);
        }

        var close = BarButton("关闭", 26, 204, 858, 200, 74);
        Wire(close, CloseLangPanel);
        box.AddChild(close);

        RefreshLangMarks();
    }

    private void OpenLangPanel()
    {
        RefreshLangMarks();
        _langPanel.Visible = true;
    }

    /// <summary>供调试参数调用（截图验收语言面板）。</summary>
    public void OpenLanguagePanel() => OpenLangPanel();

    private void CloseLangPanel() => _langPanel.Visible = false;

    private void RefreshLangMarks()
    {
        if (_langMarks == null) return;
        for (int i = 0; i < _langMarks.Length; i++)
            if (_langMarks[i] != null) _langMarks[i].Visible = (Loc.Id)i == Loc.Current;
    }

    public void ShowIntro()
    {
        _introPanel.Visible = true;
        _introBox.PivotOffset = _introBox.Size / 2f;
        _introBox.Scale = new Vector2(0.85f, 0.85f);
        _introBox.Modulate = new Color(1, 1, 1, 0);
        var t = CreateTween().SetParallel(true);
        t.TweenProperty(_introBox, "scale", Vector2.One, 0.35f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenProperty(_introBox, "modulate:a", 1f, 0.25f);
    }

    /// <summary>让控件绕基准位置上下浮动（不同时长自然错开相位）。</summary>
    private void FloatLoop(Control c, float amp, float dur)
    {
        float baseY = c.Position.Y;
        var tw = CreateTween().SetLoops();
        tw.TweenProperty(c, "position:y", baseY - amp, dur)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tw.TweenProperty(c, "position:y", baseY + amp, dur)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    private Panel AddHill(float x, float y, float w, float h, float r, Color col)
    {
        var hill = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(x, y), Size = new Vector2(w, h) };
        var sb = new StyleBoxFlat { BgColor = col };
        sb.SetCornerRadiusAll((int)r);
        hill.AddThemeStyleboxOverride("panel", sb);
        _menu.AddChild(hill);
        return hill;
    }

    /// <summary>主菜单入场：各元素带回弹的依次浮现（有曲率，不生硬）。</summary>
    private void PlayMenuIntro()
    {
        _intro?.Kill();
        _intro = CreateTween().SetParallel(true);

        _mascot.Scale = Vector2.Zero;
        _intro.TweenProperty(_mascot, "scale", Vector2.One, 0.45f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);

        _title.Modulate = new Color(1, 1, 1, 0);
        _title.Scale = new Vector2(0.86f, 0.86f);
        _intro.TweenProperty(_title, "modulate:a", 1f, 0.35f).SetDelay(0.1f);
        _intro.TweenProperty(_title, "scale", Vector2.One, 0.5f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).SetDelay(0.1f);

        for (int i = 0; i < _diffBtns.Length; i++)
        {
            _diffBtns[i].Scale = Vector2.Zero;
            _intro.TweenProperty(_diffBtns[i], "scale", Vector2.One, 0.4f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out)
                .SetDelay(0.22f + i * 0.08f);
        }

        _startBtn.Scale = Vector2.Zero;
        _intro.TweenProperty(_startBtn, "scale", Vector2.One, 0.4f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).SetDelay(0.5f);

        _exitBtn.Scale = Vector2.Zero;
        _intro.TweenProperty(_exitBtn, "scale", Vector2.One, 0.4f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out).SetDelay(0.6f);
    }

    /// <summary>蛇形游动线条：背景里的波浪"贪吃蛇"持续奔跑（替代太阳云朵的主题装饰）。</summary>
    private sealed partial class SnakeTrail : Control
    {
        private struct Trail
        {
            public float BaseY, Amp, Freq, Speed, Width, HeadR;
            public Color Col;
        }

        private float _t;
        private readonly List<Trail> _trails = new();

        public override void _Ready()
        {
            _trails.Add(new Trail { BaseY = 320, Amp = 26, Freq = 0.011f, Speed = 2.1f, Width = 10, HeadR = 13, Col = Palette.Green });
            _trails.Add(new Trail { BaseY = 590, Amp = 20, Freq = 0.009f, Speed = 2.7f, Width = 9, HeadR = 11, Col = Palette.Pink });
            _trails.Add(new Trail { BaseY = 930, Amp = 30, Freq = 0.008f, Speed = 1.8f, Width = 10, HeadR = 12, Col = Palette.Gold });
        }

        public override void _Process(double delta)
        {
            _t += (float)delta;
            QueueRedraw();
        }

        public override void _Draw()
        {
            foreach (Trail tr in _trails)
            {
                const int Seg = 44;
                var pts = new Vector2[Seg + 1];
                for (int i = 0; i <= Seg; i++)
                {
                    float x = i / (float)Seg * (Size.X + 60) - 30;
                    float y = tr.BaseY + Mathf.Sin(x * tr.Freq + _t * tr.Speed) * tr.Amp;
                    pts[i] = new Vector2(x, y);
                }

                // 分段渐显：尾巴淡、蛇头实
                for (int i = 0; i < Seg; i++)
                {
                    float k = i / (float)Seg;
                    Color c = tr.Col;
                    c.A = 0.14f + 0.4f * k;
                    DrawLine(pts[i], pts[i + 1], c, tr.Width, true);
                }

                // 蛇头：圆头 + 一对朝向前进方向的眼睛
                Vector2 head = pts[Seg];
                Vector2 prev = pts[Seg - 2];
                Vector2 dir = (head - prev).Normalized();
                Vector2 perp = new(-dir.Y, dir.X);
                DrawCircle(head, tr.HeadR, tr.Col, true, -1f, true);
                foreach (float s in new float[] { 1f, -1f })
                {
                    Vector2 ep = head + dir * tr.HeadR * 0.3f + perp * tr.HeadR * 0.42f * s;
                    DrawCircle(ep, tr.HeadR * 0.24f, Colors.White, true, -1f, true);
                    DrawCircle(ep + dir * 1.2f, tr.HeadR * 0.13f, Palette.Ink, true, -1f, true);
                }
            }
        }
    }

    // ———————————————— 暂停弹窗 ————————————————

    private void BuildPause()
    {
        _pausePanel = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _pausePanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pausePanel.Visible = false;
        _root.AddChild(_pausePanel);

        var dim = new ColorRect { Color = new Color(0.3f, 0.22f, 0.12f, 0.5f) };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _pausePanel.AddChild(dim);

        var panel = new Panel();
        panel.Position = new Vector2(120, 410);
        panel.Size = new Vector2(480, 470);
        panel.AddThemeStyleboxOverride("panel", UiKit.WoodPanel(12));
        _pausePanel.AddChild(panel);

        panel.AddChild(Sprite(UiKit.Zzz, 52, 44, 40));
        var title = TextLabel("暂停中", 48, Palette.Text);
        title.Position = new Vector2(0, 42);
        title.Size = new Vector2(480, 70);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        panel.AddChild(title);
        _pauseTitle = title;

        var resume = BarButton("继续游戏", 30, 60, 150, 360, 84);
        Wire(resume, () => Game.TogglePause());
        panel.AddChild(resume);

        var restart = BarButton("重新开始", 30, 60, 258, 360, 84);
        Wire(restart, () => { ShowPause(false); Game.StartWithTransition(Game.Diff); });
        panel.AddChild(restart);

        var menu = BarButton("回主菜单", 30, 60, 366, 360, 84);
        Wire(menu, () => Game.BackToMenuWithTransition());
        panel.AddChild(menu);
    }

    // ———————————————— 结算弹窗 ————————————————

    private void BuildOver()
    {
        _overPanel = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _overPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _overPanel.Visible = false;
        _root.AddChild(_overPanel);

        var dim = new ColorRect { Color = new Color(0.3f, 0.22f, 0.12f, 0.55f) };
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _overPanel.AddChild(dim);

        var panel = new Panel();
        panel.Position = new Vector2(100, 370);
        panel.Size = new Vector2(520, 540);
        panel.AddThemeStyleboxOverride("panel", UiKit.WoodPanel(12));
        _overPanel.AddChild(panel);
        _overBox = panel;

        _overFace = Sprite(UiKit.FaceGreen, 232, 30, 56);
        _overFace.PivotOffset = new Vector2(28, 28);
        panel.AddChild(_overFace);

        _overTitle = TextLabel("", 48, Palette.Text);
        _overTitle.Position = new Vector2(0, 96);
        _overTitle.Size = new Vector2(520, 70);
        _overTitle.HorizontalAlignment = HorizontalAlignment.Center;
        panel.AddChild(_overTitle);

        _overBadge = TextLabel("★ 新纪录 ★", 24, Palette.Gold);
        _overBadge.Position = new Vector2(0, 172);
        _overBadge.Size = new Vector2(520, 36);
        _overBadge.HorizontalAlignment = HorizontalAlignment.Center;
        panel.AddChild(_overBadge);

        _overBody = new Label
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _overBody.AddThemeFontSizeOverride("font_size", 26);
        _overBody.AddThemeFontOverride("font", UiKit.UiFont);
        _overBody.AddThemeColorOverride("font_color", Palette.Text);
        _overBody.Position = new Vector2(40, 216);
        _overBody.Size = new Vector2(440, 96);
        _overBody.HorizontalAlignment = HorizontalAlignment.Center;
        _overBody.VerticalAlignment = VerticalAlignment.Center;
        panel.AddChild(_overBody);

        var again = BarButton("再来一局", 30, 60, 326, 400, 84);
        Wire(again, () => Game.StartWithTransition(Game.Diff));
        panel.AddChild(again);
        _overAgain = again;

        var menu = BarButton("回主菜单", 30, 60, 426, 400, 84);
        Wire(menu, () => Game.BackToMenuWithTransition());
        panel.AddChild(menu);
        _overMenu = menu;
    }

    // ———————————————— 状态切换 ————————————————

    public void ShowMenu()
    {
        _menu.Visible = true;
        _hud.Visible = false;
        _pad.Visible = false;
        _pausePanel.Visible = false;
        _overPanel.Visible = false;
        UpdateSpeed();
        PlayMenuIntro();
    }

    public void EnterGameHud()
    {
        _menu.Visible = false;
        _hud.Visible = true;
        _pad.Visible = true;
        _pausePanel.Visible = false;
        _overPanel.Visible = false;
        UpdateHud();
        UpdateSpeed();
    }

    public void ShowPause(bool on)
    {
        _pausePanel.Visible = on;
    }

    public void ShowOver(bool win, string body, bool newRecord)
    {
        _overFace.Texture = win ? UiKit.FaceLove : UiKit.FaceCry;
        _overTitle.Text = Loc.T(win ? "减肥成功！" : "减肥失败…");
        _overTitle.AddThemeColorOverride("font_color", win ? Palette.LeafDark : Palette.Danger);
        _overBadge.Visible = newRecord;
        _overBody.Text = body;

        // 没有正文（胜利只显示"减肥成功"）时，面板收窄、按钮上移
        bool hasBody = !string.IsNullOrWhiteSpace(body);
        _overBody.Visible = hasBody;
        float by = hasBody ? 320f : 212f;
        _overAgain.Position = new Vector2(60, by);
        _overMenu.Position = new Vector2(60, by + 100f);
        _overBox.Size = new Vector2(520, hasBody ? 528f : 420f);
        _overBox.Position = new Vector2(100, hasBody ? 376f : 430f);

        _overPanel.Visible = true;

        // 结算面板弹入
        _overBox.PivotOffset = _overBox.Size / 2f;
        _overBox.Scale = new Vector2(0.8f, 0.8f);
        _overBox.Modulate = new Color(1, 1, 1, 0);
        var t = CreateTween().SetParallel(true);
        t.TweenProperty(_overBox, "scale", Vector2.One, 0.35f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenProperty(_overBox, "modulate:a", 1f, 0.25f);

        if (win)
        {
            var tw = CreateTween().SetLoops();
            tw.TweenProperty(_overFace, "rotation_degrees", 12f, 0.4f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tw.TweenProperty(_overFace, "rotation_degrees", -12f, 0.4f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tw.TweenProperty(_overFace, "rotation_degrees", 0f, 0.3f);
        }
    }

    public void UpdateHud()
    {
        if (Game == null || Game.Snake.Count == 0) return;
        int len = Game.Snake.Count;
        int winLen = Game.Diff.WinLength;

        string title = len >= 15 ? "肥胖" : len >= 11 ? "微胖" : len >= 8 ? "正常" : "苗条";
        _weightLabel.Text = Loc.F("体重 {0} 节 · {1}", len, Loc.T(title));

        // 液面高度 = 体重占比（越胖越满，减肥就是让液位下降）
        float k = Mathf.Clamp((float)(len - winLen) / (Game.Diff.StartLength - winLen), 0f, 1f);
        _weightBar.Progress = k;

        _appleLabel.Text = Game.FoodEaten.ToString();

        bool ready = Game.CanSpit;
        _spitBtn.Modulate = ready ? Colors.White : new Color(1, 1, 1, 0.5f);
        _spitState.Text = ready ? Loc.T("就绪 · 每步一次") : Loc.T("本步已吐过");
        _spitState.AddThemeColorOverride("font_color", ready ? Palette.LeafDark : Palette.InkSoft);
    }

    public void UpdateSpeed()
    {
        bool fast = Game.SpeedScale > 1f;
        _speedBtn.Text = fast ? "×2" : "×1";
        Color bg = fast ? new Color("b9e07f") : Palette.Cream;
        _speedBtn.AddThemeStyleboxOverride("normal", CircleStyle(56, bg));
        _speedBtn.AddThemeStyleboxOverride("hover", CircleStyle(56, bg.Lightened(0.07f)));
        _speedBtn.AddThemeStyleboxOverride("pressed", CircleStyle(56, bg.Darkened(0.16f)));
    }

    private void SelectDiff(int idx)
    {
        _selDiff = idx;
        AtlasTexture[][] rows =
        {
            new[] { UiKit.BtnCream, UiKit.BtnCreamPress },
            new[] { UiKit.BtnTan, UiKit.BtnTanPress },
            new[] { UiKit.BtnBrown, UiKit.BtnBrownPress },
        };
        for (int i = 0; i < _diffBtns.Length; i++)
        {
            bool on = i == idx;
            AtlasTexture n = on ? rows[i][0] : UiKit.BtnCream;
            _diffBtns[i].AddThemeStyleboxOverride("normal", UiKit.WoodSq(n));
            _diffBtns[i].AddThemeStyleboxOverride("hover", Darken(UiKit.WoodSq(n), 0.94f));
            _diffBtns[i].AddThemeStyleboxOverride("pressed", UiKit.WoodSq(rows[i][1]));
            _diffBadges[i].Visible = on;
        }
    }

    /// <summary>键盘 Enter 在主菜单直接开始（首次同样先看宣言）。</summary>
    public void PressStart()
    {
        if (_menu.Visible) TryStart(GameConfig.Difficulties[_selDiff]);
    }

    // ———————————————— 图形小件 ————————————————

    private static void PaintArrow(Control c, Vector2I dir)
    {
        Vector2 ctr = c.Size / 2f;
        Vector2 dv = new(dir.X, dir.Y);
        Vector2 pv = new(-dir.Y, dir.X);
        Vector2 tip = ctr + dv * 15f;
        Vector2 b1 = ctr - dv * 9f + pv * 12f;
        Vector2 b2 = ctr - dv * 9f - pv * 12f;
        c.DrawColoredPolygon(new[] { tip, b1, b2 }, new Color(0.55f, 0.42f, 0.26f));
    }

    /// <summary>纯代码绘制的小图形（方向箭头）。</summary>
    private sealed partial class ArrowGlyph : Control
    {
        public System.Action<Control> Painter;

        public override void _Draw() => Painter?.Invoke(this);
    }

    /// <summary>
    /// 摇杆底座：按住拖动转向（类王者左手轮盘）。
    /// 触摸会被模拟成鼠标事件（emulate_mouse_from_touch），因此只需处理鼠标事件。
    /// </summary>
    private sealed partial class JoystickPanel : Panel
    {
        public System.Action<Vector2I> OnDir;    // 主方向变化
        public System.Action<Vector2> OnOffset;  // 归一化偏移（长度 0~1）
        public System.Action OnRelease;

        private bool _dragging;
        private Vector2I _lastDir;

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _dragging = true;
                    Handle(mb.Position);
                    AcceptEvent();
                }
                else if (_dragging)
                {
                    _dragging = false;
                    _lastDir = Vector2I.Zero;
                    OnRelease?.Invoke();
                    AcceptEvent();
                }
            }
            else if (@event is InputEventMouseMotion mm && _dragging)
            {
                Handle(mm.Position);
                AcceptEvent();
            }
        }

        private void Handle(Vector2 local)
        {
            Vector2 d = local - Size / 2f;
            float max = Size.X * 0.34f;
            if (d.Length() > max) d = d.Normalized() * max;
            OnOffset?.Invoke(d / max);

            if (d.Length() < 12f) return; // 死区
            Vector2I dir = Mathf.Abs(d.X) >= Mathf.Abs(d.Y)
                ? (d.X > 0 ? Vector2I.Right : Vector2I.Left)
                : (d.Y > 0 ? Vector2I.Down : Vector2I.Up);
            if (dir != _lastDir)
            {
                _lastDir = dir;
                OnDir?.Invoke(dir);
            }
        }
    }
}
