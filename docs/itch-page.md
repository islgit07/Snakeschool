# itch.io 页面改造清单（AntiSnake / 贪吃蛇减肥）

页面：https://cathappygame.itch.io/snakeschool
诊断时间：2026-11-17

## 现状体检

| 项目 | 现状 | 问题 |
|---|---|---|
| 标题 | `AntiSnake` | 与 URL `snakeschool`、APK 文件名 `SnakeSchool-release.apk`、中文名「贪吃蛇减肥」四套名字并存 |
| Tagline | `A downloadable game`（默认值） | 等于没写，列表页看起来像占位 |
| 封面 | 347×500 竖版 | itch 列表按 315:250 横版裁切，竖图会被裁掉主体 |
| 截图 | **0 张** | 最致命，详情页没有任何游戏画面 |
| 描述 | 两段英文，约 60 词 | 无小标题、无玩法说明、无操作说明 |
| 标签 | `2D` `Snake` | 只有 2 个，检索面极窄 |
| 主题 | itch 默认白底红字 | 与游戏米色/绿色调完全不搭 |
| 视频 / GIF | 无 | 列表悬停不播，点击率损失最大的一项 |
| 平台 | 仅 Android，103 MB | 无浏览器可玩版本 |

## 一、先统一名字（最容易忽略）

建议主线用 **AntiSnake**（英文站内检索友好），副名保留中文：

- 页面标题：`AntiSnake` （保持）
- 描述第一行写明：`贪吃蛇减肥 / AntiSnake`
- APK 文件名改成 `AntiSnake-v0.1.0-android.apk`（重新上传）
- 封面用 `cover_en_630x500.png`

如果主要面向中文玩家，则反过来：标题 `贪吃蛇减肥`，封面用 `cover_zh_630x500.png`。**不要两套混着用**。

## 二、素材（已生成，在 export/itch/）

| 文件 | 尺寸 | 上传位置 |
|---|---|---|
| `cover_en_630x500.png` | 630×500 | Edit game → Cover image |
| `cover_zh_630x500.png` | 630×500 | 同上（二选一） |
| `banner_960x300.png` | 960×300 | Edit theme → Banner |
| `social_600x315.png` | 600×315 | Edit game → Promo images → Social media image |

用 `python tools/gen_itch_assets.py` 可重新生成（改 `SUB`、`TAG` 常量即可换文案）。

## 三、主题配色（Edit theme）

把 itch 默认白红换成游戏的米色系，页面立刻和封面成为一体：

| 位置 | 值 |
|---|---|
| Background | `#F8EDD3` |
| Text | `#5F4429` |
| Link / 按钮 | `#8FBF4D`（主绿）或 `#E2654F`（苹果红，对比更强） |

## 四、Tagline（一句话卖点）

替换默认的 `A downloadable game`，候选：

1. `Reverse Snake: eat, bloat, then vomit your way to a slimmer snake.`
2. `A snake game where eating makes you lose.`
3. `Slim the snake down — puke the fat out as obstacles.`

推荐第 1 条：有反差、说清"反向"这个唯一卖点。

## 五、描述正文

> 成稿的 HTML 版本在 `docs/itch-description.txt`，整份复制粘贴即可。
> 下面这段只是大纲，方便你改文案。

```markdown
A reverse Snake game. The snake starts fat — your job is to make it **shorter**,
not longer. Every apple you eat makes you heavier and slower, so you have to
vomit the fat out behind you and let it land as a permanent wall.

中文名：贪吃蛇减肥 · 反向贪吃蛇

## How to play

- You start fat and pink. The length you need to reach is shown at the top of the screen.
- **Swipe** on the board to steer. Hitting a wall, your own body, or a block of fat ends the run.
- Tap **VOMIT** to throw one segment of fat ahead of you — it lands and becomes a wall you can never cross again, so pick the spot before you use it.
- Shrink down to the target length to clear the round.

## The twist

- Eating an apple adds weight: the snake gets longer, slower, and turns pink
- Eat 5× your starting weight and you burst
- Past 2× weight the screen shakes, reddens, and your heart starts pounding
- Win by reaching the target length, not by scoring points

## Features

- 3 difficulties (Easy / Standard / Hell) with different starting weight and apple density
- 7 achievements, unlock popups from the top of the screen
- Built for phones: portrait, one-thumb play

## Notes

- Android only (arm64), ~103 MB — that's the .NET runtime, not assets
- Mobile only for now; no desktop or browser build yet
- Chinese UI only; English is on the way

## Feedback

Found a bug or a balance issue? Leave a comment — I read everything.
```

## 六、还缺的两件事（优先级最高）

1. **3–5 张截图**。itch 的截图栏是页面唯一的"游戏长什么样"证据，0 张 = 只有封面图。建议 1280×720 横版，或统一竖版；至少：主菜单、游戏中（胖蛇）、催吐瞬间、结算页。
2. **一张 GIF**（≤3 MB，建议 480–640 宽 / 8–12 秒 / 30fps 循环）。itch 列表里鼠标悬停会自动播放，这是点击率提升最大的一项。

截图我可以帮你从游戏里抓（需要短暂运行一次游戏窗口），GIF 可以在抓到之后用 ffmpeg 合成。

## 七、可选：包体积

当前 103 MB 主要是 .NET/Mono 运行时 + Godot 引擎本体，素材占比很小。可以顺手做的：

- `export_presets.cfg` 里把 `dotnet/include_debug_symbols` 设为 `false`，去掉 pdb（省几 MB）
- 想进一步压缩只能换语言重写（Godot 的 C# 版本目前不支持导出 Web，所以暂时也没法做浏览器可玩版）
