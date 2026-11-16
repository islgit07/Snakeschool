# 贪吃蛇减肥（SnakeSchool）

反向贪吃蛇：蛇开局**最胖**，目标不是吃——而是**催吐减肥**。吃苹果会让体重上涨、身躯迟缓；
主动催吐会把尾部的肥肉抛到蛇头前方变成路障。把体重减到目标节数即通关。

![gameplay](https://img.shields.io/badge/Godot-4.7.2%20mono-478cbf)

## 玩法

| 操作 | 键盘 | 触屏 |
| --- | --- | --- |
| 转向 | WASD / 方向键 | 滑动棋盘 · 拖动方向键中央圆盘 |
| 催吐 | 空格 | 催吐大按钮（每步限一次） |
| 暂停 | P | 暂停按钮 |
| 倍速 | 右上角 ×1/×2 | 同左 |
| 重开（结算时） | R | 再来一局 |

- 吃苹果：体重 +N，越来越胖越来越慢
- 催吐：尾部少一节，肥肉从蛇头抛物线飞出，落地成永久障碍
- 警报：体重达到开局 2 倍起，屏幕摇晃 + 红色暗角 + 心跳音
- 结算：胜利只看「减肥成功」（数据不上屏），失败显示死因；最少苹果/最快用时入存档

## 难度

| 难度 | 开局→目标 | 苹果增重 | 场上苹果 | 爬速 | 肉块落点 |
| --- | --- | --- | --- | --- | --- |
| 轻松 | 14 → 8 节 | +2 | 1 个 | 慢 | 飞得远 |
| 标准 | 18 → 6 节 | +3 | 3 个 | 中速 | 中等 |
| 地狱 | 24 → 5 节 | +4 | 5 个 | 极速 | 砸在脚边 |

## 运行

1. 安装 [Godot 4.7.2 mono](https://godotengine.org/download)（需 .NET 版本）与 .NET 8 SDK
2. `dotnet build SnakeSchool.csproj`
3. 用 Godot 打开工程直接运行，或 `godot --path .`

调试参数（`--` 后追加）：`skipmenu`（跳过菜单）、`state=win|lose|paused|fat`、`fxtest`（循环转场演示）。

## 技术

- Godot 4.7.2 mono / C# / .NET 8，竖屏 720×1280，`canvas_items` 拉伸自适应
- 全部 UI 代码构建：`scripts/UiLayer.cs`（摇杆 / 液态体重条 / 木牌按钮）
- 程序化音效与 BGM：`scripts/AudioBank.cs`（无外部音频文件）
- 素材：[Sprout Lands UI Pack（免费版）](https://cupnooble.itch.io/sprout-lands-asset-pack)（仅限非商业使用）、字体 字由文艺黑 + 站酷庆科黄油体
- 转场 shader 来自 [MrEliptik/godot_ui_components](https://github.com/MrEliptik/godot_ui_components)（MIT）

## 目录

```
scenes/Main.tscn        场景入口（Main / GameBoard / UiLayer / TransitionFx / Audio）
scripts/Main.cs         状态机、回合步进、催吐规则、难度与输入
scripts/GameBoard.cs    棋盘渲染：平滑蠕动蛇、粒子、飞行动画、肥胖摇晃
scripts/UiLayer.cs      菜单 / HUD / 虚拟按键 / 摇杆 / 弹窗 / 警报暗角
scripts/GameConfig.cs   全局参数与难度预设
scripts/UiKit.cs        素材图集切片、字体、九宫格样式
scripts/Audio*.cs       音效合成与播放、存档
assets/                 字体与素材子集（构建必需）
```
