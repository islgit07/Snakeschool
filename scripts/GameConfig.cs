using Godot;

/// <summary>
/// 「贪吃蛇减肥」全局规则表：地图布局、难度预设、催吐规则、素材路径集中在这里。
/// </summary>
public static class GameConfig
{
    // —— 地图 ——
    public const int GridSize = 34;
    public const int GridW = 20;
    public const int GridH = 20;
    public const int BoardPx = GridSize * GridW;

    // —— 设计分辨率（与 project.godot 保持一致）——
    public const int DesignW = 720;
    public const int DesignH = 1280;
    public const float BoardOffsetX = (DesignW - BoardPx) / 2f;
    public const float BoardOffsetY = 120f;

    // —— 催吐规则：完全由玩家主动触发，不再自动吐 ——
    // 肉块落点距离改为随难度变化（见 DifficultyPreset），飞行时长全局统一
    public const float SpitFlightTime = 0.24f;

    public static Vector2 CellCenter(Vector2I c) =>
        new(c.X * GridSize + GridSize / 2f, c.Y * GridSize + GridSize / 2f);

    // —— 难度预设（体验区分：爬速 / 场上苹果数 / 苹果增重 / 肉块落点 / 目标体重）——
    public static readonly DifficultyPreset[] Difficulties =
    {
        new("轻松", "开局 14 节 · 目标 8 节 · 苹果 +2 · 场上 1 个苹果 · 慢速 · 肉块飞得远",
            14, 8, 2, 1, 5, 0.22f, 0.008f),
        new("标准", "开局 18 节 · 目标 6 节 · 苹果 +3 · 场上 3 个苹果 · 中速",
            18, 6, 3, 3, 4, 0.15f, 0.012f),
        new("地狱", "开局 24 节 · 目标 5 节 · 苹果 +4 · 场上 5 个苹果 · 极速 · 肉块砸在脚边",
            24, 5, 4, 5, 3, 0.075f, 0.009f),
    };
}

/// <summary>一档难度的全部数值。</summary>
public sealed class DifficultyPreset
{
    public readonly string Name;
    public readonly string Desc;
    public readonly int StartLength;
    public readonly int WinLength;
    public readonly int FoodFatGain;
    public readonly int MaxFood;
    public readonly int SpitDistance;       // 肉块落在蛇头前方第几格（靠墙自动往回收）
    public readonly float BaseStepInterval;
    public readonly float StepPerSegment;

    public DifficultyPreset(string name, string desc, int startLength, int winLength,
        int foodFatGain, int maxFood, int spitDistance, float baseStep, float stepPerSeg)
    {
        Name = name; Desc = desc;
        StartLength = startLength; WinLength = winLength;
        FoodFatGain = foodFatGain; MaxFood = maxFood;
        SpitDistance = spitDistance;
        BaseStepInterval = baseStep; StepPerSegment = stepPerSeg;
    }

    public float StepIntervalFor(int length) => BaseStepInterval + length * StepPerSegment;
}

/// <summary>全局配色：Sprout Lands 暖色农场风，棋盘与界面共用。</summary>
public static class Palette
{
    public static readonly Color Accent = new("8fbf4d");    // 主绿（苗条）
    public static readonly Color Green = new("8fbf4d");
    public static readonly Color LeafDark = new("5e8f33");
    public static readonly Color Pink = new("f2a0bd");      // 肥胖粉
    public static readonly Color Blossom = new("f2a0bd");
    public static readonly Color Gold = new("f2c94c");
    public static readonly Color Apple = new("e2654f");
    public static readonly Color Danger = new("e06c5a");
    public static readonly Color Text = new("5f4429");      // 深棕文字
    public static readonly Color SubText = new("9a7b58");
    public static readonly Color Ink = new("5f4429");       // 描边棕
    public static readonly Color InkSoft = new("9a7b58");
    public static readonly Color Cream = new("fff6df");
    public static readonly Color Wood = new("d9b078");
    public static readonly Color BgTop = new("f8edd3");
    public static readonly Color BgBottom = new("ecd9ae");
    public static readonly Color BoardTop = new("f6e7c4");
    public static readonly Color BoardBottom = new("eddab2");
}
