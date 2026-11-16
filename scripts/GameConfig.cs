using Godot;

/// <summary>
/// 厌食蛇全局规则表：所有数值集中在这里，改难度只动这一个文件。
/// </summary>
public static class GameConfig
{
    // 地图
    public const int GridSize = 32;
    public const int GridW = 20;
    public const int GridH = 20;

    // 体重规则
    public const int StartLength = 18;  // 开局体重（节）
    public const int WinLength = 6;     // 瘦到多少节算赢
    public const int FoodFatGain = 3;   // 吃到一个苹果长胖几节
    public const int MaxFood = 3;       // 地图上同时存在几个苹果

    // 呕吐规则
    public const float AutoSpitInterval = 2.5f; // 每 2.5 秒自动吐一节
    public const int SpitDistance = 4;          // 肉块最远吐到蛇头前方第几格

    // 速度曲线：越胖越迟钝（步进间隔越长），越瘦越灵活
    public const float BaseStepInterval = 0.12f;
    public const float StepIntervalPerSegment = 0.012f;

    public static float StepIntervalFor(int length)
    {
        return BaseStepInterval + length * StepIntervalPerSegment;
    }
}
