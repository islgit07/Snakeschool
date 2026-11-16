using Godot;
using System.Collections.Generic;

/// <summary>
/// 成就系统：定义、解锁判定辅助与持久化（user://achievements.cfg）。
/// 弹出动画在 UiLayer（灵动岛式，从顶部滑入）。
/// </summary>
public static class Achievements
{
    private const string SavePath = "user://achievements.cfg";

    public static readonly (string Id, string Name)[] All =
    {
        ("modest", "致死都装作矜持"),         // 开局 60 秒不吃也不吐
        ("stone_foot", "搬起石头砸自己的脚"), // 被自己吐出的肥肉撞死
        ("dragon", "全场游龙"),               // 二倍速状态下连续存活 30 秒
        ("reverse_clock", "反方向的钟"),      // 按下与行进完全相反的方向
        ("ouroboros", "衔尾蛇"),              // 咬到最末端的尾巴而死
        ("vip", "健身房VIP客户"),             // 30 秒内减肥成功
        ("konami", "科乐美秘技"),             // 依次按下 上上下下左右左右
    };

    private static readonly HashSet<string> _unlocked = Load();

    public static string NameOf(string id)
    {
        foreach ((string i, string name) in All)
            if (i == id) return name;
        return id;
    }

    public static bool Unlocked(string id) => _unlocked.Contains(id);

    /// <summary>解锁成就，返回是否为新解锁（重复解锁不弹）。</summary>
    public static bool Unlock(string id)
    {
        if (_unlocked.Contains(id)) return false;
        _unlocked.Add(id);
        Save();
        return true;
    }

    private static HashSet<string> Load()
    {
        var set = new HashSet<string>();
        var cf = new ConfigFile();
        if (cf.Load(SavePath) == Error.Ok)
        {
            foreach (string key in cf.GetSectionKeys("unlocked") ?? System.Array.Empty<string>())
                set.Add(key);
        }
        return set;
    }

    private static void Save()
    {
        var cf = new ConfigFile();
        foreach (string id in _unlocked)
            cf.SetValue("unlocked", id, true);
        cf.Save(SavePath);
    }
}
