using Godot;
using System.Collections.Generic;

public enum GameState { Menu, Playing, Paused, Won, Lost }
public enum DeathCause { None, Wall, Obstacle, SelfBite }

/// <summary>
/// 游戏主控：状态机、回合步进、输入（键盘 + 触摸滑动）、主动催吐规则与结算。
/// 画面交给 GameBoard，界面交给 UiLayer，音效交给 AudioManager。
/// </summary>
public partial class Main : Node2D
{
    public GameState State = GameState.Menu;
    public DeathCause DeathCause = DeathCause.None;

    public AudioManager Audio { get; private set; }
    public GameBoard Board { get; private set; }
    public UiLayer Ui { get; private set; }
    public TransitionFx Fx { get; private set; }

    public DifficultyPreset Diff = GameConfig.Difficulties[1];

    private readonly List<Vector2I> _snake = new();
    private readonly List<Vector2I> _prevSnake = new();          // 上一步位置，用于平滑插值
    private readonly HashSet<Vector2I> _obstacles = new();
    private readonly List<Vector2I> _foods = new();
    private readonly HashSet<Vector2I> _flyingTargets = new();   // 飞行中肉块的落点占位，防止重复吐到同一格

    private Vector2I _dir = Vector2I.Right;
    private Vector2I _nextDir = Vector2I.Right;
    private float _stepTimer;
    private bool _spitUsedThisStep;

    private int _foodEaten;
    private float _runTime;
    private float _deathFade;

    private bool _swiping;
    private Vector2 _swipeFrom;

    /// <summary>游戏时间倍率：右上角按钮切换 1x / 2x。</summary>
    public float SpeedScale { get; private set; } = 1f;

    /// <summary>
    /// 肥胖警报强度：体重达到开局 2 倍起警，2.5 倍满格（用于晃动/红暗角/心跳音）。
    /// </summary>
    public float FatAlarm
    {
        get
        {
            int start = Diff.StartLength;
            if (_snake.Count == 0) return 0f;
            return Mathf.Clamp((float)(_snake.Count - 2 * start) / (0.5f * start), 0f, 1f);
        }
    }

    private float _heartTimer = 99f;

    // —— 供 GameBoard / UiLayer 读取 ——
    public IReadOnlyList<Vector2I> Snake => _snake;
    public IReadOnlyList<Vector2I> PrevSnake => _prevSnake;
    public IReadOnlyList<Vector2I> Foods => _foods;
    public HashSet<Vector2I> Obstacles => _obstacles;
    public Vector2I Dir => _dir;
    public int FoodEaten => _foodEaten;
    public int RunSeconds => (int)_runTime;
    public float DeathFade => Mathf.Clamp(_deathFade, 0f, 1f);
    public float MoveT => Mathf.Clamp(_stepTimer / Mathf.Max(0.01f, Diff.StepIntervalFor(_snake.Count)), 0f, 1f);
    public bool CanSpit => State == GameState.Playing && !_spitUsedThisStep && _snake.Count > Diff.WinLength;

    public override void _Ready()
    {
        Audio = GetNode<AudioManager>("Audio");
        Board = GetNode<GameBoard>("GameBoard");
        Ui = GetNode<UiLayer>("UiLayer");
        Fx = GetNode<TransitionFx>("TransitionFx");
        Board.Game = this;
        Ui.Game = this;

        Board.MenuDemo = true;
        Ui.ShowMenu();
        ApplyDebugArgs();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        if (State == GameState.Playing)
        {
            HandleKeyboard();
            _runTime += dt;
            _stepTimer += dt * SpeedScale;
            if (_stepTimer >= Diff.StepIntervalFor(_snake.Count))
            {
                _stepTimer -= Diff.StepIntervalFor(_snake.Count);
                Step();
            }

            // 胖到两倍以后：心跳声随警报强度越来越急
            float alarm = FatAlarm;
            if (alarm > 0f)
            {
                _heartTimer += dt;
                float period = Mathf.Lerp(0.85f, 0.42f, alarm);
                if (_heartTimer >= period)
                {
                    _heartTimer = 0f;
                    Audio.Play(AudioBank.Land, 0.55f, -8f);
                }
            }
            else
            {
                _heartTimer = 99f;
            }
        }
        else if (State == GameState.Lost && _deathFade > 0f)
        {
            _deathFade -= dt * 1.4f;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true } key)
        {
            if (key.Keycode == Key.P && (State == GameState.Playing || State == GameState.Paused))
                TogglePause();
            else if (key.Keycode == Key.R && (State == GameState.Won || State == GameState.Lost))
                StartGame(Diff);
            else if (key.Keycode == Key.Enter && State == GameState.Menu)
                Ui.PressStart();
            return;
        }

        // 触摸滑动转向（桌面端开启"鼠标模拟触摸"后同样生效）
        if (State != GameState.Playing) return;
        if (@event is InputEventScreenTouch t)
        {
            _swiping = t.Pressed;
            _swipeFrom = t.Position;
        }
        else if (@event is InputEventScreenDrag d && _swiping)
        {
            Vector2 delta = d.Position - _swipeFrom;
            if (delta.Length() < 52f) return;
            _swipeFrom = d.Position; // 支持不抬手的连续滑动
            SetDir(Mathf.Abs(delta.X) >= Mathf.Abs(delta.Y)
                ? (delta.X > 0 ? Vector2I.Right : Vector2I.Left)
                : (delta.Y > 0 ? Vector2I.Down : Vector2I.Up));
        }
    }

    private void HandleKeyboard()
    {
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) SetDir(Vector2I.Up);
        else if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) SetDir(Vector2I.Down);
        else if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) SetDir(Vector2I.Left);
        else if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) SetDir(Vector2I.Right);

        if (Input.IsKeyPressed(Key.Space)) TrySpit();
    }

    // ———————————————— 输入动作 ————————————————

    public void SetDir(Vector2I d)
    {
        if (State != GameState.Playing) return;
        if (d == _dir || d == -_dir) return;
        _nextDir = d;
        Audio.PlayMinor(AudioBank.Turn);
    }

    /// <summary>
    /// 主动催吐：把尾巴一节吐出去，肉块飞到蛇头当前方向前方 SpitDistance 格（靠近墙/被占时自动往回收）。
    /// 每走一步限吐一次，防止站桩连吐秒赢。
    /// </summary>
    public bool TrySpit()
    {
        if (State != GameState.Playing) return false;
        if (!CanSpit) { Audio.PlayMinor(AudioBank.Denied); return false; }

        Vector2I head = _snake[0];
        Vector2I land = default;
        bool found = false;
        for (int k = Diff.SpitDistance; k >= 1; k--)
        {
            Vector2I cand = head + _dir * k;
            if (cand.X < 0 || cand.X >= GameConfig.GridW || cand.Y < 0 || cand.Y >= GameConfig.GridH) continue;
            if (_obstacles.Contains(cand) || _foods.Contains(cand) || _flyingTargets.Contains(cand)) continue;
            land = cand;
            found = true;
            break;
        }
        if (!found) { Audio.PlayMinor(AudioBank.Denied); return false; }

        // 尾巴少一节，肉块从蛇头"吐"出去，飞向落点
        _snake.RemoveAt(_snake.Count - 1);
        if (_prevSnake.Count > _snake.Count) _prevSnake.RemoveAt(_prevSnake.Count - 1);

        _flyingTargets.Add(land);
        Board.LaunchChunk(head, land);
        Audio.Play(AudioBank.Spit, (float)GD.RandRange(0.9, 1.1));
        _spitUsedThisStep = true;
        Ui.UpdateHud();

        if (_snake.Count <= Diff.WinLength) Win();
        return true;
    }

    public void TogglePause()
    {
        if (State == GameState.Playing)
        {
            State = GameState.Paused;
            Ui.ShowPause(true);
            Audio.PlayMinor(AudioBank.Click);
        }
        else if (State == GameState.Paused)
        {
            State = GameState.Playing;
            Ui.ShowPause(false);
            Audio.PlayMinor(AudioBank.Click);
        }
    }

    // ———————————————— 流程 ————————————————

    public void StartGame(DifficultyPreset diff)
    {
        Diff = diff;
        _snake.Clear();
        _prevSnake.Clear();
        _obstacles.Clear();
        _foods.Clear();
        _flyingTargets.Clear();

        foreach (Vector2I c in BuildSpawnSnake(diff.StartLength)) _snake.Add(c);
        _prevSnake.AddRange(_snake);
        _dir = _nextDir = Vector2I.Right;
        _stepTimer = 0f;
        _spitUsedThisStep = false;
        _foodEaten = 0;
        _runTime = 0f;
        _deathFade = 0f;
        DeathCause = DeathCause.None;

        for (int i = 0; i < diff.MaxFood; i++) SpawnFood();

        Board.MenuDemo = false;
        Board.OnNewRun();
        State = GameState.Playing;
        Ui.EnterGameHud();
    }

    public void BackToMenu()
    {
        State = GameState.Menu;
        Board.OnNewRun();
        Board.MenuDemo = true;
        Ui.ShowMenu();
    }

    /// <summary>带波浪转场的开局 / 回菜单。</summary>
    public void StartWithTransition(DifficultyPreset diff) => Fx.Run(() => StartGame(diff));

    public void BackToMenuWithTransition() => Fx.Run(BackToMenu);

    /// <summary>切换 1x / 2x 游戏速度。</summary>
    public void ToggleSpeed()
    {
        SpeedScale = SpeedScale > 1f ? 1f : 2f;
        Ui.UpdateSpeed();
    }

    /// <summary>
    /// 开局蛇形铺位：头排从头往左铺满，剩余的铺到下一行，保证全部落在棋盘内，
    /// 且蛇头朝右有足够的活动空间（原版直排开局会把尾巴伸到棋盘外）。
    /// </summary>
    private static List<Vector2I> BuildSpawnSnake(int len)
    {
        var s = new List<Vector2I>();
        const int headX = 5, rowY = 10;
        int first = Mathf.Min(len, headX + 1);
        for (int i = 0; i < first; i++) s.Add(new Vector2I(headX - i, rowY));
        int rest = len - first;
        for (int i = 0; i < rest; i++) s.Add(new Vector2I(i, rowY + 1));
        return s;
    }

    // ———————————————— 回合步进 ————————————————

    private void Step()
    {
        _prevSnake.Clear();
        _prevSnake.AddRange(_snake);

        _dir = _nextDir;
        Vector2I newHead = _snake[0] + _dir;

        // 死法一：撞墙
        if (newHead.X < 0 || newHead.X >= GameConfig.GridW
            || newHead.Y < 0 || newHead.Y >= GameConfig.GridH)
        {
            Die(DeathCause.Wall);
            return;
        }
        // 死法二：撞上自己吐出的肥肉障碍
        if (_obstacles.Contains(newHead))
        {
            Die(DeathCause.Obstacle);
            return;
        }
        // 死法三：咬到自己
        if (_snake.Contains(newHead))
        {
            Die(DeathCause.SelfBite);
            return;
        }

        _snake.Insert(0, newHead);

        if (_foods.Remove(newHead))
        {
            _foodEaten++;
            for (int i = 0; i < Diff.FoodFatGain; i++)
                _snake.Add(_snake[^1]); // 尾部原地复制：变长
            Audio.Play(AudioBank.Eat, (float)GD.RandRange(0.95, 1.1));
            Board.OnEat(GameConfig.CellCenter(newHead), Diff.FoodFatGain);
            SpawnFood();
        }
        else
        {
            _snake.RemoveAt(_snake.Count - 1);
        }

        _spitUsedThisStep = false; // 每走一步恢复一次催吐机会
        Ui.UpdateHud();

        if (_snake.Count <= Diff.WinLength) Win();
    }

    /// <summary>随机刷苹果：避开蛇身、肥肉、已有苹果和飞行落点。</summary>
    private void SpawnFood()
    {
        for (int i = 0; i < 300; i++)
        {
            Vector2I p = new(
                (int)GD.RandRange(0, GameConfig.GridW - 1),
                (int)GD.RandRange(0, GameConfig.GridH - 1));
            if (_snake.Contains(p) || _obstacles.Contains(p) || _foods.Contains(p) || _flyingTargets.Contains(p)) continue;
            _foods.Add(p);
            return;
        }
    }

    /// <summary>飞行肉块落地：变成永久障碍。</summary>
    public void OnChunkLanded(Vector2I cell)
    {
        _flyingTargets.Remove(cell);
        if (State is GameState.Won or GameState.Lost) return; // 结算后落地的肉块不再生成障碍
        _obstacles.Add(cell);
        Audio.Play(AudioBank.Land, (float)GD.RandRange(0.9, 1.15));
    }

    // ———————————————— 结算 ————————————————

    private void Win()
    {
        State = GameState.Won;
        bool newRecord = Audio.ReportWin(_foodEaten, _runTime);
        Audio.Play(AudioBank.Win);
        Board.CelebrateWin();
        Ui.ShowOver(true, "", newRecord); // 只显示"减肥成功"，不展示数据
    }

    private void Die(DeathCause cause)
    {
        DeathCause = cause;
        State = GameState.Lost;
        _deathFade = 1f;
        Audio.Play(AudioBank.Die);
        Board.OnDeath();

        string reason = cause switch
        {
            DeathCause.Wall => "撞上了围墙。",
            DeathCause.Obstacle => "被自己吐出的肥肉绊倒了。",
            DeathCause.SelfBite => "咬到了自己的尾巴。",
            _ => "",
        };
        Ui.ShowOver(false, reason, false); // 只显示失败原因，不展示数据
    }

    // ———————————————— 调试参数（截图验收用） ————————————————

    private async void LoopTestTransitions()
    {
        while (IsInsideTree())
        {
            await ToSignal(GetTree().CreateTimer(4.0), "timeout");
            Fx.Run(null, 2.5f); // 盖 0.8s → 掀 0.8s，循环触发
        }
    }

    private void ApplyDebugArgs()
    {
        var args = OS.GetCmdlineUserArgs();
        GD.Print($"[debug] user args: [{string.Join(", ", args)}]");
        foreach (string a in args)
        {
            if (a == "skipmenu") StartGame(Diff);
            else if (a == "state=win")
            {
                StartGame(Diff);
                while (_snake.Count > Diff.WinLength + 1) _snake.RemoveAt(_snake.Count - 1);
                Win();
            }
            else if (a == "state=lose") { StartGame(Diff); Die(DeathCause.SelfBite); }
            else if (a == "state=paused") { StartGame(Diff); TogglePause(); }
            else if (a == "state=fat")
            {
                // 模拟胖到两倍以上：验证肥胖警报（摇晃/红暗角/心跳）
                StartGame(Diff);
                for (int i = 0; i < 24; i++) _snake.Add(_snake[^1]);
            }
            else if (a == "fxtest")
            {
                // 纯视觉测试：进游戏后循环触发放慢的转场（不切状态），方便截图
                StartGame(Diff);
                LoopTestTransitions();
            }
        }
    }
}
