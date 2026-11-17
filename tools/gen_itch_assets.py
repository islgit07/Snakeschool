"""SnakeSchool —— itch.io 页面素材生成器。

按 itch 官方尺寸产出：
  - cover_630x500.png   封面（列表页展示，比例 315:250）
  - banner_960x300.png  页面顶部横幅
  - social_600x315.png  社交分享卡

复用 gen_icon.py 的绿头绘制与游戏调色板，保证和图标/游戏内风格一致。
"""

import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gen_icon import (INK, GREEN, CREAM, BG_TOP, BG_BOTTOM, PINK,
                      draw_head, lerp, lighten)

APPLE = (0xE2, 0x65, 0x4F)
GOLD = (0xF2, 0xC9, 0x4C)

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONT_TITLE = os.path.join(ROOT, "assets", "fonts", "ZCOOLQingKeHuangYou-Regular.ttf")
FONT_BODY = os.path.join(ROOT, "assets", "fonts", "wenyi_hei.ttf")
OUT_DIR = os.path.join(ROOT, "export", "itch")

SUB = "反向贪吃蛇 · 越吃越胖，学会催吐"
TAG = "Godot 4 · C#"


def bg_panel(w, h, radius=0.0, grid=True, grad=True):
    """米色底 + 可选棋盘点阵（呼应贪吃蛇的格子）。"""
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    if grad:
        g = Image.radial_gradient("L").resize((w, h), Image.LANCZOS)
        base = Image.composite(Image.new("RGB", (w, h), BG_BOTTOM),
                               Image.new("RGB", (w, h), CREAM),
                               g.point(lambda v: int(v * 0.65))).convert("RGBA")
    else:
        base = Image.new("RGBA", (w, h), BG_TOP + (255,))

    if grid:
        dots = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        d = ImageDraw.Draw(dots)
        step = max(18, int(min(w, h) * 0.055))
        r = max(1, step // 14)
        for y in range(step, h, step):
            for x in range(step, w, step):
                d.ellipse([x - r, y - r, x + r, y + r], fill=(0x5F, 0x44, 0x29, 26))
        base.alpha_composite(dots)

    if radius > 0:
        mask = Image.new("L", (w, h), 0)
        ImageDraw.Draw(mask).rounded_rectangle([0, 0, w - 1, h - 1],
                                               radius=int(min(w, h) * radius), fill=255)
        out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        out.paste(base, (0, 0), mask)
        return out
    return base


def fit_font(path, text, max_w, start):
    size = start
    while size > 8:
        f = ImageFont.truetype(path, size)
        if ImageDraw.Draw(Image.new("RGB", (10, 10))).textlength(text, font=f) <= max_w:
            return f
        size -= 2
    return ImageFont.truetype(path, 8)


def text_with_shadow(img, xy, text, font, fill, shadow=(255, 255, 255, 150), off=3, anchor="la"):
    d = ImageDraw.Draw(img)
    d.text((xy[0] + off, xy[1] + off), text, font=font, fill=shadow, anchor=anchor)
    d.text(xy, text, font=font, fill=fill, anchor=anchor)


def pill(img, cx, cy, text, font, fg, bg, pad_x=None, pad_y=None):
    d = ImageDraw.Draw(img)
    tw = d.textlength(text, font=font)
    th = font.size
    px = pad_x if pad_x is not None else th * 0.55
    py = pad_y if pad_y is not None else th * 0.34
    box = [cx - tw / 2 - px, cy - th / 2 - py, cx + tw / 2 + px, cy + th / 2 + py]
    d.rounded_rectangle(box, radius=(box[3] - box[1]) / 2, fill=bg, outline=INK + (255,), width=3)
    d.text((cx, cy), text, font=font, fill=fg, anchor="mm")


def apple(base, cx, cy, r):
    """小红苹果（带描边与高光），独立于图标里的实现，尺寸更可控。"""
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    ow = r * 0.14
    for dx in (-r * 0.26, r * 0.26):
        d.ellipse([cx + dx - r * 0.82 - ow, cy - r * 0.95 - ow,
                   cx + dx + r * 0.82 + ow, cy + r * 0.95 + ow], fill=INK + (255,))
    for dx in (-r * 0.26, r * 0.26):
        d.ellipse([cx + dx - r * 0.82, cy - r * 0.95, cx + dx + r * 0.82, cy + r * 0.95],
                  fill=APPLE + (255,))
    d.ellipse([cx + r * 0.26 - r * 0.82, cy - r * 0.95, cx + r * 0.26 + r * 0.82, cy + r * 0.95],
              fill=APPLE + (255,))
    hl = Image.new("RGBA", base.size, (0, 0, 0, 0))
    ImageDraw.Draw(hl).ellipse([cx - r * 0.52, cy - r * 0.58, cx - r * 0.06, cy - r * 0.10],
                               fill=(255, 255, 255, 165))
    layer.alpha_composite(hl.filter(ImageFilter.GaussianBlur(r * 0.10)))
    # 蒂
    d = ImageDraw.Draw(layer)
    d.line([cx + r * 0.02, cy - r * 0.86, cx + r * 0.14, cy - r * 1.12],
           fill=INK + (255,), width=int(r * 0.17))
    base.alpha_composite(layer)


def make_cover(title="贪吃蛇减肥", sub=SUB, title_font=None, w=1260, h=1000):
    """封面：上 60% 一颗大头 + 苹果，下 40% 标题区，互不重叠。"""
    img = bg_panel(w, h)

    head = draw_head(h, head_w=0.46)                 # 头宽约为画面 46% 的短边
    img.alpha_composite(head, ((w - h) // 2, int(h * 0.305) - int(h * 0.515)))
    apple(img, int(w * 0.800), int(h * 0.268), int(h * 0.058))

    f_title = fit_font(title_font or FONT_TITLE, title, w * 0.80, int(h * 0.175))
    text_with_shadow(img, (w / 2, h * 0.722), title, f_title, INK + (255,),
                     shadow=(255, 255, 255, 170), off=max(2, int(h * 0.005)), anchor="ms")

    f_sub = fit_font(FONT_BODY, sub, w * 0.80, int(h * 0.062))
    ImageDraw.Draw(img).text((w / 2, h * 0.838), sub, font=f_sub,
                             fill=(0x9A, 0x7B, 0x58, 255), anchor="mm")

    return img


def make_banner(w=1920, h=600):
    """横幅：左侧完整一颗头（不裁切），右侧标题 + 标语 + 标签，左对齐。"""
    img = bg_panel(w, h, radius=0.09)

    head = draw_head(h, head_w=0.70)                  # 头高约 0.61h，四周留白
    img.alpha_composite(head, (int(h * 0.13), int(-h * 0.015)))

    x = int(h * 1.16)
    avail = w - x - int(h * 0.34)
    f_title = fit_font(FONT_TITLE, "贪吃蛇减肥", avail, int(h * 0.295))
    text_with_shadow(img, (x, h * 0.315), "贪吃蛇减肥", f_title, INK + (255,),
                     shadow=(255, 255, 255, 160), off=3, anchor="lm")

    f_sub = fit_font(FONT_BODY, SUB, avail, int(h * 0.105))
    ImageDraw.Draw(img).text((x, h * 0.605), SUB, font=f_sub,
                             fill=(0x9A, 0x7B, 0x58, 255), anchor="lm")

    # 标签胶囊左对齐到 x
    f_tag = fit_font(FONT_BODY, TAG, avail * 0.6, int(h * 0.072))
    d = ImageDraw.Draw(img)
    tw = d.textlength(TAG, font=f_tag)
    px = f_tag.size * 0.55
    pill(img, x + tw / 2 + px, h * 0.775, TAG, f_tag, CREAM + (255,), GREEN + (255,))

    apple(img, int(w - h * 0.24), int(h * 0.225), int(h * 0.065))
    return img


def make_social(w=1200, h=630):
    img = bg_panel(w, h)
    head = draw_head(h, head_w=0.46)
    img.alpha_composite(head, (int(w * 0.04), int(h * 0.10)))
    left = w * 0.50
    avail = w * 0.94 - left
    f_title = fit_font(FONT_TITLE, "贪吃蛇减肥", avail, int(h * 0.20))
    text_with_shadow(img, (left, h * 0.40), "贪吃蛇减肥", f_title, INK + (255,),
                     shadow=(255, 255, 255, 160), off=3, anchor="lm")
    f_sub = fit_font(FONT_BODY, SUB, avail, int(h * 0.076))
    ImageDraw.Draw(img).text((left, h * 0.625), SUB, font=f_sub,
                             fill=(0x9A, 0x7B, 0x58, 255), anchor="lm")
    return img


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    latin = "C:/Windows/Fonts/ariblk.ttf"
    if not os.path.exists(latin):
        latin = None
    jobs = [
        ("cover_zh_630x500.png", make_cover(), (630, 500)),
        ("cover_en_630x500.png",
         make_cover(title="AntiSnake",
                    sub="reverse snake · eat, bloat, vomit to slim down",
                    title_font=latin), (630, 500)),
        ("banner_960x300.png", make_banner(), (960, 300)),
        ("social_600x315.png", make_social(), (600, 315)),
    ]
    for name, img, size in jobs:
        img.resize(size, Image.LANCZOS).save(os.path.join(OUT_DIR, name))
        print("saved", name, os.path.getsize(os.path.join(OUT_DIR, name)) // 1024, "KB")


if __name__ == "__main__":
    main()
