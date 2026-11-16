using Godot;
using System.Collections.Generic;

public enum GameState { Playing, Won, Lost }
public enum DeathCause { None, Wall, Obstacle, SelfBite }

public partial class Main : Node2D
{
    private Vector2I _dir = Vector2I.Right;
    private Vector2I _nextDir = Vector2I.Right;

    private List<Vector2I> _snake = new();
    private HashSet<Vector2I> _obstacles = new();
    private List<Vector2I> _foods = new();
    private HashSet<Vector2I> _flyingChunks = new(); // 本帧刚吐出、还在飞的肉块（简化：直接落点）

    private float _stepTimer = 0f;
    private float _spitTimer = 0f;
    private GameState _state = GameState.Playing;
    private DeathCause _deathCause = DeathCause.None;

    private Label _statusLabel;
    private Label _overLabel;
    private int _foodEaten = 0;

    public override void _Ready()
    {
        _statusLabel = new Label();
        _overLabel = new Label();
        _statusLabel.Position = new Vector2(10, 8);
        _overLabel.Position = new Vector2(140, 290);
        _overLabel.Visible = false;
        AddChild(_statusLabel);
        AddChild(_overLabel);

        ResetRun(); // 必须在 Label 创建之后：ResetRun 里会碰 _overLabel
    }

    public override void _Process(double delta)
    {
        HandleInput();
        if (_state != GameState.Playing) { QueueRedraw(); return; }

        float dt = (float)delta;

        _stepTimer += dt;
        if (_stepTimer >= GameConfig.StepIntervalFor(_snake.Count))
        {
            _stepTimer = 0f;
            Step();
        }

        _spitTimer += dt;
        if (_spitTimer >= GameConfig.AutoSpitInterval)
        {
            _spitTimer = 0f;
            Spit();
        }

        UpdateStatus();
        QueueRedraw();
    }

    private void HandleInput()
    {
        if (_state != GameState.Playing)
        {
            if (Input.IsKeyPressed(Key.R)) ResetRun();
            return;
        }

        if (Input.IsKeyPressed(Key.W) && _dir != Vector2I.Down) _nextDir = Vector2I.Up;
        else if (Input.IsKeyPressed(Key.S) && _dir != Vector2I.Up) _nextDir = Vector2I.Down;
        else if (Input.IsKeyPressed(Key.A) && _dir != Vector2I.Right) _nextDir = Vector2I.Left;
        else if (Input.IsKeyPressed(Key.D) && _dir != Vector2I.Left) _nextDir = Vector2I.Right;

        if (Input.IsKeyPressed(Key.Space)) Spit();
    }

    private void ResetRun()
    {
        _snake.Clear();
        _obstacles.Clear();
        _foods.Clear();
        for (int i = 0; i < GameConfig.StartLength; i++)
            _snake.Add(new Vector2I(10 - i, 10));
        _dir = Vector2I.Right;
        _nextDir = Vector2I.Right;
        _stepTimer = 0f;
        _spitTimer = 0f;
        _foodEaten = 0;
        _state = GameState.Playing;
        _deathCause = DeathCause.None;
        for (int i = 0; i < GameConfig.MaxFood; i++) PlaceFood();
        _overLabel.Visible = false;
    }

    private void Step()
    {
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
            for (int i = 0; i < GameConfig.FoodFatGain; i++)
                _snake.Add(_snake[^1]); // 尾部原地复制：变长
            PlaceFood();
        }
        else
        {
            _snake.RemoveAt(_snake.Count - 1);
        }

        // 胜利判定
        if (_snake.Count <= GameConfig.WinLength)
        {
            _state = GameState.Won;
            ShowEnd(true);
        }
    }

    /// <summary>
    /// 催吐：把尾巴一节吐出去，落在蛇头前方 SpitDistance 格（越界则贴墙）。
    /// 吐出的肉块变成永久障碍物。
    /// </summary>
    private void Spit()
    {
        if (_snake.Count <= GameConfig.WinLength || _state != GameState.Playing) return;

        Vector2I tail = _snake[^1];
        _snake.RemoveAt(_snake.Count - 1);

        Vector2I land = _snake[0] + _dir * GameConfig.SpitDistance;
        land.X = Mathf.Clamp(land.X, 0, GameConfig.GridW - 1);
        land.Y = Mathf.Clamp(land.Y, 0, GameConfig.GridH - 1);
        _obstacles.Add(land);
        _flyingChunks.Add(land); // 简化处理：落点直接生效，飞行只是表现
    }

    private void PlaceFood()
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            Vector2I pos = new(
                (int)GD.RandRange(0, GameConfig.GridW - 1),
                (int)GD.RandRange(0, GameConfig.GridH - 1));
            if (_snake.Contains(pos) || _obstacles.Contains(pos) || _foods.Contains(pos)) continue;
            _foods.Add(pos);
            return;
        }
    }

    private void Die(DeathCause cause)
    {
        _deathCause = cause;
        _state = GameState.Lost;
        ShowEnd(false);
    }

    private void ShowEnd(bool won)
    {
        string title = won ? "★ 你瘦下来了！ ★" : "游戏结束";
        string reason = _deathCause switch
        {
            DeathCause.Wall => "你撞墙了。",
            DeathCause.Obstacle => "你被自己吐出的肥肉噎死了。",
            DeathCause.SelfBite => "你咬到了自己。",
            _ => ""
        };
        _overLabel.Text = won
            ? $"{title}\n吃了 {_foodEaten} 个苹果，吐了 {GameConfig.StartLength - _snake.Count} 节"
            : $"{title}\n{reason}\n体重 {_snake.Count} 节（目标 ≤{GameConfig.WinLength}）\n按 R 重开";
        _overLabel.Visible = true;
    }

    private void UpdateStatus()
    {
        int len = _snake.Count;
        string title = len >= 15 ? "肥胖" : len >= 11 ? "微胖" : len >= 8 ? "正常" : "苗条";
        _statusLabel.Text = $"体重: {len} 节（{title}）  目标 ≤{GameConfig.WinLength}  苹果: {_foodEaten}";
    }

    public override void _Draw()
    {
        // 地板
        DrawRect(new Rect2(0, 0, GameConfig.GridW * GameConfig.GridSize, GameConfig.GridH * GameConfig.GridSize),
            new Color(0.13f, 0.13f, 0.16f));

        // 网格线（淡）
        Color gridLine = new(1, 1, 1, 0.03f);
        for (int x = 0; x <= GameConfig.GridW; x++)
            DrawLine(new Vector2(x * GameConfig.GridSize, 0),
                new Vector2(x * GameConfig.GridSize, GameConfig.GridH * GameConfig.GridSize), gridLine);
        for (int y = 0; y <= GameConfig.GridH; y++)
            DrawLine(new Vector2(0, y * GameConfig.GridSize),
                new Vector2(GameConfig.GridW * GameConfig.GridSize, y * GameConfig.GridSize), gridLine);

        // 吐出的肥肉障碍（暗肉色，带高光边）
        foreach (Vector2I ob in _obstacles)
        {
            Rect2 r = CellRect(ob);
            DrawRect(r, new Color(0.45f, 0.25f, 0.2f));
            DrawRect(new Rect2(r.Position + new Vector2(4, 4), r.Size - new Vector2(8, 8)),
                new Color(0.62f, 0.38f, 0.3f));
        }

        // 苹果（亮红圆）
        foreach (Vector2I food in _foods)
        {
            Vector2 center = CellCenter(food);
            DrawCircle(center, GameConfig.GridSize * 0.35f, new Color(0.85f, 0.2f, 0.2f));
            DrawCircle(center + new Vector2(-3, -3), GameConfig.GridSize * 0.1f, new Color(1f, 0.6f, 0.6f));
        }

        // 蛇身：从粉（胖）到绿（瘦）渐变，头部黄
        int len = _snake.Count;
        float t = Mathf.Clamp((float)(len - GameConfig.WinLength) / (GameConfig.StartLength - GameConfig.WinLength), 0f, 1f);
        Color bodyColor = new Color(0.3f, 0.85f, 0.45f).Lerp(new Color(0.95f, 0.55f, 0.75f), t);

        for (int i = _snake.Count - 1; i >= 0; i--)
        {
            Color c = i == 0 ? new Color(0.92f, 0.85f, 0.3f) : bodyColor;
            Rect2 r = CellRect(_snake[i]);
            DrawRect(r, c);
        }
    }

    private Rect2 CellRect(Vector2I cell)
    {
        return new Rect2(cell.X * GameConfig.GridSize + 1, cell.Y * GameConfig.GridSize + 1,
            GameConfig.GridSize - 2, GameConfig.GridSize - 2);
    }

    private Vector2 CellCenter(Vector2I cell)
    {
        return new Vector2(cell.X * GameConfig.GridSize + GameConfig.GridSize / 2f,
            cell.Y * GameConfig.GridSize + GameConfig.GridSize / 2f);
    }
}
