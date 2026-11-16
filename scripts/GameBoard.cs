using Godot;
using System.Collections.Generic;

/// <summary>
/// 游戏画面渲染：奶油棋盘、木质边框、平滑蠕动的蛇（双层描边）、
/// 苹果 sprite、肥肉块、飞行肉块、粒子与飘字。只负责"画"，游戏数据从 Main 读取。
/// </summary>
public partial class GameBoard : Node2D
{
    public Main Game;

    // —— 特效数据 ——
    private struct Particle { public Vector2 Pos, Vel; public float Life, Max, Size, Gravity, Spin; public Color Col; public bool Square; }
    private struct FloatText { public Vector2 Pos; public string Text; public float Life, Max, Size; public Color Col; }
    private struct Flight { public Vector2 From, To; public float T, Dur; public Vector2I Land; }

    private readonly List<Particle> _parts = new();
    private readonly List<FloatText> _texts = new();
    private readonly List<Flight> _flights = new();

    private float _time, _shake, _flash;
    private Vector2 _basePos;

    private StyleBoxTexture _frameSb;
    private StyleBoxFlat _meatOuterSb, _meatInnerSb;

    // —— 主菜单演示蛇 ——
    private readonly List<Vector2I> _demoSnake = new();
    private readonly List<Vector2I> _demoPrev = new();
    private Vector2I _demoDir = Vector2I.Right;
    private float _demoStepTimer;
    private bool _menuDemo;

    public bool MenuDemo
    {
        get => _menuDemo;
        set { _menuDemo = value; if (value) InitDemo(); }
    }

    public override void _Ready()
    {
        _basePos = new Vector2(GameConfig.BoardOffsetX, GameConfig.BoardOffsetY);
        Position = _basePos;
        TextureFilter = TextureFilterEnum.Nearest; // 像素素材保持锐利

        _frameSb = UiKit.WoodPanel(12);
        _meatOuterSb = Rounded(new Color(0.78f, 0.60f, 0.38f), 9, new Color(0.48f, 0.34f, 0.20f), 2);
        _meatInnerSb = Rounded(new Color(0.90f, 0.74f, 0.50f), 6);
    }

    private static StyleBoxFlat Rounded(Color bg, int r, Color? border = null, int bw = 0)
    {
        var sb = new StyleBoxFlat { BgColor = bg };
        sb.SetCornerRadiusAll(r);
        if (border.HasValue) { sb.BorderColor = border.Value; sb.SetBorderWidthAll(bw); }
        return sb;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;

        if (_menuDemo) DemoStep(dt);
        UpdateFx(dt);

        // 屏幕震动：死亡爆震 + 肥胖低频摇晃（两倍体重起晃，越胖越明显）
        float fat = (Game != null && !_menuDemo && Game.State == GameState.Playing) ? Game.FatAlarm : 0f;
        float rumble = fat * fat * 4.0f;
        Vector2 jitter = new(
            Mathf.Sin(_time * 11.7f) + Mathf.Sin(_time * 23.1f) * 0.5f,
            Mathf.Cos(_time * 9.3f) + Mathf.Sin(_time * 19.7f) * 0.5f);
        Vector2 offset = _shake > 0f
            ? new Vector2((float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(-1.0, 1.0)) * _shake
            : Vector2.Zero;
        Position = _basePos + jitter * (rumble * 0.6f) + offset;
        _shake = Mathf.Max(0f, _shake - dt * 32f);
        _flash = Mathf.Max(0f, _flash - dt * 2.2f);

        QueueRedraw();
    }

    // ———————————————— 对外特效接口 ————————————————

    /// <summary>新一局：清空全部特效。</summary>
    public void OnNewRun()
    {
        _parts.Clear();
        _texts.Clear();
        _flights.Clear();
        _shake = 0f;
        _flash = 0f;
    }

    /// <summary>催吐：肉块从蛇头飞向落点。</summary>
    public void LaunchChunk(Vector2I fromCell, Vector2I landCell)
    {
        _flights.Add(new Flight
        {
            From = GameConfig.CellCenter(fromCell),
            To = GameConfig.CellCenter(landCell),
            T = 0f,
            Dur = GameConfig.SpitFlightTime,
            Land = landCell,
        });
    }

    /// <summary>肉块落地的小尘土。</summary>
    public void PoofAt(Vector2I cell)
    {
        Vector2 p = GameConfig.CellCenter(cell);
        for (int i = 0; i < 9; i++)
        {
            float ang = (float)GD.RandRange(0.0, Mathf.Pi * 2.0);
            float spd = (float)GD.RandRange(60.0, 170.0);
            float life = (float)GD.RandRange(0.35, 0.65);
            _parts.Add(new Particle
            {
                Pos = p,
                Vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd,
                Life = life,
                Max = life,
                Size = (float)GD.RandRange(3.0, 6.0),
                Gravity = 320f,
                Col = new Color(0.72f, 0.58f, 0.38f, 0.9f),
            });
        }
    }

    /// <summary>吃到苹果：红金火花 + "+N" 飘字。</summary>
    public void OnEat(Vector2 center, int fatGain)
    {
        for (int i = 0; i < 12; i++)
        {
            float ang = (float)GD.RandRange(0.0, Mathf.Pi * 2.0);
            float spd = (float)GD.RandRange(70.0, 240.0);
            float life = (float)GD.RandRange(0.3, 0.6);
            Color col = GD.Randf() < 0.5f ? Palette.Apple : Palette.Gold;
            _parts.Add(new Particle
            {
                Pos = center,
                Vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd,
                Life = life,
                Max = life,
                Size = (float)GD.RandRange(2.5, 5.0),
                Gravity = 260f,
                Col = new Color(col.R, col.G, col.B, 0.95f),
            });
        }
        AddText(center + new Vector2(0, -14), $"+{fatGain}", Palette.Danger, 26);
    }

    /// <summary>死亡：震屏 + 红闪 + 蛇身爆裂成粒子。</summary>
    public void OnDeath()
    {
        _shake = 14f;
        _flash = 1f;
        var snake = Game.Snake;
        for (int i = 0; i < snake.Count; i++)
        {
            Vector2 p = GameConfig.CellCenter(snake[i]);
            Color col = Palette.Green.Lerp(Palette.Pink, (float)i / Mathf.Max(1, snake.Count));
            for (int j = 0; j < 4; j++)
            {
                float ang = (float)GD.RandRange(0.0, Mathf.Pi * 2.0);
                float spd = (float)GD.RandRange(90.0, 280.0);
                float life = (float)GD.RandRange(0.5, 0.9);
                _parts.Add(new Particle
                {
                    Pos = p,
                    Vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * spd,
                    Life = life,
                    Max = life,
                    Size = (float)GD.RandRange(5.0, 9.0),
                    Gravity = 480f,
                    Col = new Color(col.R, col.G, col.B, 0.95f),
                });
            }
        }
    }

    /// <summary>通关：全场彩带雨。</summary>
    public void CelebrateWin()
    {
        Color[] confetti = { Palette.Gold, Palette.Green, Palette.Pink, Palette.Apple, Palette.Cream };
        for (int i = 0; i < 80; i++)
        {
            float life = (float)GD.RandRange(1.5, 2.4);
            _parts.Add(new Particle
            {
                Pos = new Vector2((float)GD.RandRange(0.0, GameConfig.BoardPx), (float)GD.RandRange(-60.0, -5.0)),
                Vel = new Vector2((float)GD.RandRange(-50.0, 50.0), (float)GD.RandRange(70.0, 150.0)),
                Life = life,
                Max = life,
                Size = (float)GD.RandRange(4.5, 8.5),
                Gravity = 420f,
                Spin = (float)GD.RandRange(-8.0, 8.0),
                Col = confetti[GD.RandRange(0, confetti.Length - 1)],
                Square = true,
            });
        }
    }

    private void AddText(Vector2 pos, string text, Color col, float size)
    {
        _texts.Add(new FloatText { Pos = pos, Text = text, Life = 0.9f, Max = 0.9f, Size = size, Col = col });
    }

    // ———————————————— 特效更新 ————————————————

    private void UpdateFx(float dt)
    {
        for (int i = _parts.Count - 1; i >= 0; i--)
        {
            Particle p = _parts[i];
            p.Pos += p.Vel * dt;
            p.Vel = new Vector2(p.Vel.X, p.Vel.Y + p.Gravity * dt);
            p.Life -= dt;
            if (p.Life <= 0f) _parts.RemoveAt(i); else _parts[i] = p;
        }

        for (int i = _texts.Count - 1; i >= 0; i--)
        {
            FloatText t = _texts[i];
            t.Life -= dt;
            if (t.Life <= 0f) _texts.RemoveAt(i); else _texts[i] = t;
        }

        for (int i = _flights.Count - 1; i >= 0; i--)
        {
            Flight f = _flights[i];
            f.T += dt / f.Dur;
            if (f.T >= 1f)
            {
                _flights.RemoveAt(i);
                Game.OnChunkLanded(f.Land);
                PoofAt(f.Land);
                continue;
            }
            _flights[i] = f;
            // 飞行拖尾
            Vector2 p = f.From.Lerp(f.To, f.T);
            p.Y -= Mathf.Sin(f.T * Mathf.Pi) * 26f;
            _parts.Add(new Particle
            {
                Pos = p,
                Vel = new Vector2(0, 30),
                Life = 0.25f,
                Max = 0.25f,
                Size = 4.5f,
                Gravity = 0f,
                Col = new Color(0.72f, 0.58f, 0.38f, 0.7f),
            });
        }
    }

    // ———————————————— 绘制 ————————————————

    public override void _Draw()
    {
        // 木质外框
        DrawStyleBox(_frameSb, new Rect2(-16, -16, GameConfig.BoardPx + 32, GameConfig.BoardPx + 32));

        // 奶油渐变地板 + 淡棋盘格 + 网格线
        for (int y = 0; y < GameConfig.GridH; y++)
        {
            Color rowCol = Palette.BoardTop.Lerp(Palette.BoardBottom, y / (float)GameConfig.GridH);
            DrawRect(new Rect2(0, y * GameConfig.GridSize, GameConfig.BoardPx, GameConfig.GridSize), rowCol);
        }
        Color checker = new(0.45f, 0.33f, 0.18f, 0.05f);
        for (int x = 0; x < GameConfig.GridW; x++)
            for (int y = 0; y < GameConfig.GridH; y++)
                if ((x + y) % 2 == 0)
                    DrawRect(new Rect2(x * GameConfig.GridSize, y * GameConfig.GridSize, GameConfig.GridSize, GameConfig.GridSize), checker);

        Color gridLine = new(0.45f, 0.33f, 0.18f, 0.10f);
        for (int x = 0; x <= GameConfig.GridW; x++)
            DrawLine(new Vector2(x * GameConfig.GridSize, 0), new Vector2(x * GameConfig.GridSize, GameConfig.BoardPx), gridLine);
        for (int y = 0; y <= GameConfig.GridH; y++)
            DrawLine(new Vector2(0, y * GameConfig.GridSize), new Vector2(GameConfig.BoardPx, y * GameConfig.GridSize), gridLine);

        DrawObstacles();
        DrawFoods();

        if (MenuDemo)
        {
            DrawWorm(_demoSnake, _demoPrev, DemoT, Palette.Green, Palette.Gold, 0.45f, _demoDir);
        }
        else if (Game != null)
        {
            DrawGameSnake();
        }

        DrawFlights();
        DrawParticles();
        DrawTexts();

        if (_flash > 0f)
            DrawRect(new Rect2(-16, -16, GameConfig.BoardPx + 32, GameConfig.BoardPx + 32),
                new Color(1f, 0.35f, 0.25f, _flash * 0.3f));
    }

    private void DrawObstacles()
    {
        if (MenuDemo || Game == null) return;
        foreach (Vector2I ob in Game.Obstacles)
        {
            Rect2 r = new(ob.X * GameConfig.GridSize + 1, ob.Y * GameConfig.GridSize + 1,
                GameConfig.GridSize - 2, GameConfig.GridSize - 2);
            DrawStyleBox(_meatOuterSb, r);
            DrawStyleBox(_meatInnerSb, r.Grow(-6));
            Vector2 c = GameConfig.CellCenter(ob);
            DrawCircle(c + new Vector2(-5, -4), 2.2f, new Color(0.48f, 0.34f, 0.2f, 0.8f));
            DrawCircle(c + new Vector2(4, 5), 1.8f, new Color(0.48f, 0.34f, 0.2f, 0.8f));
        }
    }

    private void DrawFoods()
    {
        var foods = MenuDemo ? null : Game?.Foods;
        if (foods == null) return;
        foreach (Vector2I food in foods)
        {
            Vector2 c = GameConfig.CellCenter(food);
            float bob = Mathf.Sin(_time * 3f + food.X * 1.7f) * 1.8f;

            // 影子
            DrawCircle(new Vector2(c.X, GameConfig.CellCenter(food).Y + 11), 7.5f, new Color(0.45f, 0.33f, 0.18f, 0.18f));
            // 苹果 sprite（16px 原生 → 32px 棋盘内，nearest 缩放保持像素感）
            DrawTextureRect(UiKit.Apple, new Rect2(c.X - 16, c.Y - 16 + bob, 32, 32), false);
        }
    }

    private void DrawGameSnake()
    {
        var snake = Game.Snake;
        if (snake.Count == 0) return;
        float alpha = Game.State == GameState.Lost ? Game.DeathFade : 1f;
        if (alpha <= 0f) return;

        float fat = Mathf.Clamp(
            (float)(snake.Count - Game.Diff.WinLength) / Mathf.Max(1, Game.Diff.StartLength - Game.Diff.WinLength), 0f, 1f);
        Color bodyCol = Palette.Green.Lerp(Palette.Pink, fat);
        DrawWorm(snake, Game.PrevSnake, Game.MoveT, bodyCol, Palette.Gold, alpha, Game.Dir);
    }

    /// <summary>
    /// 把蛇画成一条圆润的"蠕虫"：先画深棕描边层，再画身体层，
    /// 节与节之间用粗线相连，头部带眼睛和舌头。
    /// </summary>
    private void DrawWorm(IReadOnlyList<Vector2I> cur, IReadOnlyList<Vector2I> prev, float t,
        Color body, Color headCol, float alpha, Vector2I dir)
    {
        int n = cur.Count;
        if (n == 0) return;

        Vector2 Center(int idx)
        {
            Vector2I c = cur[idx];
            Vector2I p = idx < prev.Count ? prev[idx] : c;
            return new Vector2(
                Mathf.Lerp(p.X, c.X, t) * GameConfig.GridSize + GameConfig.GridSize / 2f,
                Mathf.Lerp(p.Y, c.Y, t) * GameConfig.GridSize + GameConfig.GridSize / 2f);
        }

        float radius = GameConfig.GridSize * 0.40f;
        Color outline = Palette.Ink;
        outline.A = alpha;

        // 描边层（略粗一圈）
        for (int i = n - 1; i >= 1; i--)
        {
            Vector2 a = Center(i), b = Center(i - 1);
            DrawLine(a, b, outline, (radius + 3f) * 2f, true);
            DrawCircle(a, radius + 3f, outline, true, -1f, true);
        }
        DrawCircle(Center(0), radius + 5f, outline, true, -1f, true);

        // 身体层：越靠尾部颜色越深
        for (int i = n - 1; i >= 1; i--)
        {
            float k = i / (float)Mathf.Max(1, n - 1);
            Color c = body.Lerp(Palette.LeafDark, k * 0.45f);
            c.A = alpha;
            Vector2 a = Center(i);
            Vector2 b = Center(i - 1);
            DrawLine(a, b, c, radius * 2f, true);
            DrawCircle(a, radius, c, true, -1f, true);
        }

        // 头
        Vector2 h = Center(0);
        Color hc = headCol;
        hc.A = alpha;
        DrawCircle(h, radius + 1f, hc, true, -1f, true);

        // 眼睛（朝运动方向）
        Vector2 dv = new(dir.X, dir.Y);
        Vector2 pv = new(-dir.Y, dir.X);
        foreach (float s in new float[] { 1f, -1f })
        {
            Vector2 ep = h + dv * 5f + pv * 6.5f * s;
            Color wc = Colors.White; wc.A = alpha;
            Color bc = Palette.Ink; bc.A = alpha;
            DrawCircle(ep, 3.6f, wc, true, -1f, true);
            DrawCircle(ep + dv * 1.4f, 1.9f, bc, true, -1f, true);
        }

        // 舌头一伸一缩
        if (Mathf.Sin(_time * 7f) > 0f)
        {
            Color tc = new(1f, 0.42f, 0.55f, alpha);
            Vector2 tip = h + dv * (radius + 8f);
            DrawLine(h + dv * radius * 0.6f, tip, tc, 2.5f, true);
            DrawLine(tip, tip + pv * 3f - dv * 2f, tc, 2f, true);
            DrawLine(tip, tip - pv * 3f - dv * 2f, tc, 2f, true);
        }
    }

    private void DrawFlights()
    {
        foreach (Flight f in _flights)
        {
            Vector2 p = f.From.Lerp(f.To, f.T);
            p.Y -= Mathf.Sin(f.T * Mathf.Pi) * 26f; // 抛物线
            DrawSetTransform(p, f.T * 9f, Vector2.One);
            DrawStyleBox(_meatOuterSb, new Rect2(-13, -13, 26, 26));
            DrawStyleBox(_meatInnerSb, new Rect2(-8, -8, 16, 16));
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }

    private void DrawParticles()
    {
        foreach (Particle p in _parts)
        {
            float a = Mathf.Clamp(p.Life / p.Max, 0f, 1f);
            Color c = p.Col;
            c.A *= a;
            float size = p.Size * (0.4f + 0.6f * a);
            if (p.Square)
            {
                DrawSetTransform(p.Pos, p.Spin * _time, Vector2.One);
                DrawRect(new Rect2(-size / 2, -size / 2, size, size), c);
                DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            }
            else
            {
                DrawCircle(p.Pos, size, c);
            }
        }
    }

    private void DrawTexts()
    {
        foreach (FloatText t in _texts)
        {
            float a = Mathf.Clamp(t.Life / t.Max, 0f, 1f);
            Vector2 pos = t.Pos + new Vector2(0, -(1 - a) * 22f);
            Color shadow = new(1f, 0.97f, 0.88f, a * 0.85f);
            Color col = t.Col;
            col.A = a;
            DrawString(UiKit.UiFont, pos + new Vector2(2, 2), t.Text,
                HorizontalAlignment.Left, -1, (int)t.Size, shadow);
            DrawString(UiKit.UiFont, pos, t.Text,
                HorizontalAlignment.Left, -1, (int)t.Size, col);
        }
    }

    // ———————————————— 菜单演示蛇 ————————————————

    private void InitDemo()
    {
        _demoSnake.Clear();
        for (int i = 0; i < 12; i++) _demoSnake.Add(new Vector2I(12 - i, 6));
        _demoPrev.Clear();
        _demoPrev.AddRange(_demoSnake);
        _demoDir = Vector2I.Right;
        _demoStepTimer = 0f;
    }

    private void DemoStep(float dt)
    {
        _demoStepTimer += dt;
        if (_demoStepTimer < 0.12f) return;
        _demoStepTimer = 0f;

        _demoPrev.Clear();
        _demoPrev.AddRange(_demoSnake);

        // 随机尝试转向
        if (GD.Randf() < 0.3f)
        {
            Vector2I turn = GD.Randf() < 0.5f
                ? new Vector2I(_demoDir.Y, _demoDir.X)
                : new Vector2I(-_demoDir.Y, -_demoDir.X);
            if (DemoFree(_demoSnake[0] + turn)) _demoDir = turn;
        }

        Vector2I next = _demoSnake[0] + _demoDir;
        if (!DemoFree(next))
        {
            Vector2I[] turns = { new(_demoDir.Y, _demoDir.X), new(-_demoDir.Y, -_demoDir.X) };
            foreach (Vector2I tt in turns)
            {
                if (DemoFree(_demoSnake[0] + tt))
                {
                    _demoDir = tt;
                    next = _demoSnake[0] + tt;
                    break;
                }
            }
        }

        if (DemoFree(next))
        {
            _demoSnake.Insert(0, next);
            _demoSnake.RemoveAt(_demoSnake.Count - 1);
        }
    }

    private bool DemoFree(Vector2I c) =>
        c.X >= 0 && c.X < GameConfig.GridW && c.Y >= 0 && c.Y < GameConfig.GridH && !_demoSnake.Contains(c);

    private float DemoT => Mathf.Clamp(_demoStepTimer / 0.12f, 0f, 1f);
}
