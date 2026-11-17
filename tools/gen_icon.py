"""贪吃蛇减肥 —— 启动图标生成器（极简版：米色圆角底 + 一颗小绿头）。

程序化绘制，无外部素材依赖，任意尺寸都锐利：
    1024 画布绘制 -> LANCZOS 降采样到各尺寸。

用法:
    python tools/gen_icon.py            # 生成预览到 export/icon_preview/
    python tools/gen_icon.py build      # 覆盖 assets/icons 全套（项目图标 + Android 自适应）
"""

import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

# ── 调色板（与 scripts/GameConfig.cs 的 Palette 一致） ──
INK = (0x5F, 0x44, 0x29)
GREEN = (0x8F, 0xBF, 0x4D)
CREAM = (0xFF, 0xF6, 0xDF)
BG_TOP = (0xF8, 0xED, 0xD3)
BG_BOTTOM = (0xEC, 0xD9, 0xAE)
PINK = (0xF2, 0xA0, 0xBD)

W = 1024                 # 绘制分辨率
RADIUS = 0.225           # 圆角半径（相对画布，iOS squircle 比例）
HEAD_W = 0.445           # 头部宽度（相对画布）


def lerp(c1, c2, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(round(a + (b - a) * t)) for a, b in zip(c1, c2))


def lighten(c, t):
    return lerp(c, (255, 255, 255), t)


def rounded_bg(size, radius=RADIUS):
    """米色圆角底：径向暖色渐变，四角透明。"""
    g = Image.radial_gradient("L").resize((size, size), Image.LANCZOS)
    bg = Image.composite(Image.new("RGB", (size, size), BG_BOTTOM),
                         Image.new("RGB", (size, size), CREAM), g.point(lambda v: int(v * 0.72)))

    mask = Image.new("L", (size, size), 0)
    r = int(size * radius)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, size - 1, size - 1], radius=r, fill=255)

    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.paste(bg, (0, 0), mask)
    return out, mask


def draw_head(size, head_w=HEAD_W):
    """一颗正面朝向的小绿头（带墨棕描边、眼睛、柔光），透明底。"""
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    cx, cy = size * 0.5, size * 0.515
    hw = size * head_w * 0.5
    hh = hw * 0.87
    ow = size * 0.0245                       # 描边粗细

    # 投影
    sh = Image.new("L", (size, size), 0)
    ImageDraw.Draw(sh).ellipse([cx - hw * 0.98, cy - hh * 0.55 + size * 0.045,
                                cx + hw * 0.98, cy + hh * 1.02 + size * 0.045], fill=90)
    sh = sh.filter(ImageFilter.GaussianBlur(size * 0.030))
    shl = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    shl.paste((*INK, 255), (0, 0), sh)
    layer.alpha_composite(shl)

    # 头：墨棕外圈 + 绿填充
    d.ellipse([cx - hw - ow, cy - hh - ow, cx + hw + ow, cy + hh + ow], fill=INK + (255,))
    d.ellipse([cx - hw, cy - hh, cx + hw, cy + hh], fill=GREEN + (255,))

    # 顶部柔光
    gl = Image.new("L", (size, size), 0)
    ImageDraw.Draw(gl).ellipse([cx - hw * 0.98, cy - hh * 1.04, cx + hw * 0.62, cy + hh * 0.30], fill=120)
    gl = gl.filter(ImageFilter.GaussianBlur(size * 0.036))
    body_mask = Image.new("L", (size, size), 0)
    ImageDraw.Draw(body_mask).ellipse([cx - hw, cy - hh, cx + hw, cy + hh], fill=255)
    gl = Image.composite(gl, Image.new("L", (size, size), 0), body_mask)
    glossy = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    glossy.putalpha(gl.point(lambda v: int(v * 0.55)))
    layer.alpha_composite(glossy)

    # 眼睛
    er = size * 0.0705
    for side in (-1, 1):
        ex = cx + side * size * 0.0885
        ey = cy - size * 0.052
        d.ellipse([ex - er * 1.19, ey - er * 1.19, ex + er * 1.19, ey + er * 1.19], fill=INK + (255,))
        d.ellipse([ex - er, ey - er, ex + er, ey + er], fill=(255, 255, 255, 255))
        pr = er * 0.55
        px, py = ex + side * er * 0.06, ey + er * 0.10
        d.ellipse([px - pr, py - pr, px + pr, py + pr], fill=INK + (255,))
        hr = pr * 0.38
        d.ellipse([px - pr * 0.36 - hr, py - pr * 0.42 - hr,
                   px - pr * 0.36 + hr, py - pr * 0.42 + hr], fill=(255, 255, 255, 245))

    # 小嘴：一道短短的弧线
    mr = hw * 0.30
    my = cy + hh * 0.40
    d.arc([cx - mr, my - mr * 0.86, cx + mr, my + mr * 0.86],
          start=18, end=162, fill=INK + (255,), width=max(2, int(size * 0.0125)))

    # 腮红（很淡，只给一点温度）
    bl = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    bd = ImageDraw.Draw(bl)
    for side in (-1, 1):
        bx = cx + side * hw * 0.74
        by = cy + hh * 0.30
        br = size * 0.045
        bd.ellipse([bx - br, by - br, bx + br, by + br], fill=PINK + (95,))
    bl = bl.filter(ImageFilter.GaussianBlur(size * 0.020))
    bl.putalpha(Image.composite(bl.getchannel("A"), Image.new("L", (size, size), 0), body_mask))
    layer.alpha_composite(bl)
    return layer


def compose(size=W):
    """圆角米色底 + 居中绿头。返回 RGBA。"""
    bg, mask = rounded_bg(size)
    bg.alpha_composite(draw_head(size))
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.paste(bg, (0, 0), mask)
    return out


def build(root):
    icons = os.path.join(root, "assets", "icons")
    os.makedirs(icons, exist_ok=True)

    full = compose(W)

    # 1) 项目图标 / Android 旧版图标（圆角、四角透明）
    full.resize((512, 512), Image.LANCZOS).save(os.path.join(icons, "icon.png"))
    full.resize((192, 192), Image.LANCZOS).save(os.path.join(icons, "icon_192.png"))

    # 1b) Windows EXE 用的多尺寸 .ico
    full.resize((256, 256), Image.LANCZOS).save(
        os.path.join(icons, "icon.ico"),
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])

    # 2) 自适应图标：前景只要绿头（透明底），头部占画布 56%，稳落在 66% 安全区内
    draw_head(W, head_w=0.56).resize((432, 432), Image.LANCZOS).save(
        os.path.join(icons, "fg_432.png"))

    # 3) 自适应背景：满幅米色（不留圆角，交给桌面做遮罩）
    bg, _ = rounded_bg(W, radius=0.0)
    bg.resize((432, 432), Image.LANCZOS).save(os.path.join(icons, "bg_432.png"))

    # 预览
    prev = os.path.join(root, "export", "icon_preview")
    os.makedirs(prev, exist_ok=True)
    full.resize((512, 512), Image.LANCZOS).convert("RGB").save(os.path.join(prev, "icon_512.png"))
    for s_ in (96, 48):
        full.resize((s_, s_), Image.LANCZOS).save(os.path.join(prev, f"icon_{s_}.png"))
    print("built: assets/icons/{icon.png,icon_192.png,fg_432.png,bg_432.png}")


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    mode = sys.argv[1] if len(sys.argv) > 1 else "build"
    if mode == "build":
        build(root)
    else:
        prev = os.path.join(root, "export", "icon_preview")
        os.makedirs(prev, exist_ok=True)
        compose(W).resize((512, 512), Image.LANCZOS).save(os.path.join(prev, "icon_preview.png"))
        print("preview saved")


if __name__ == "__main__":
    main()
