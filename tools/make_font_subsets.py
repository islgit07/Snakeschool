"""为繁体中文/日/韩/泰生成 Noto 子集字体。

只保留 LocTable 里对应语言实际用到的字符 + ASCII + 常用标点，
体积从几 MB 压到几十 KB，且字重与界面一致（Bold）。

用法: python tools/make_font_subsets.py
"""

import os
import re
import subprocess
import sys
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC_DIR = os.path.join(HERE, "font_src")
OUT_DIR = os.path.join(ROOT, "assets", "fonts")

PYFTSUBSET = r"C:\Users\Administrator\AppData\Local\Programs\Python\Python312\Scripts\pyftsubset.exe"

# 语言 -> (LocTable 里的字典名, 源字体 URL, 源文件名, 输出名)
JOBS = {
    "ja": ("Ja",
           "https://github.com/notofonts/noto-cjk/raw/main/Sans/SubsetOTF/JP/NotoSansJP-Bold.otf",
           "NotoSansJP-Bold.otf", "noto_jp_subset.otf"),
    "ko": ("Ko",
           "https://github.com/notofonts/noto-cjk/raw/main/Sans/SubsetOTF/KR/NotoSansKR-Bold.otf",
           "NotoSansKR-Bold.otf", "noto_kr_subset.otf"),
    # 繁体优先用圆体「粉圆」（与主字体 文艺黑 的圆润风格一致），Noto TC 作备份
    "zh_TW": ("ZhHant",
              "https://raw.githubusercontent.com/google/fonts/main/ofl/huninn/Huninn-Regular.ttf",
              "Huninn-Regular.ttf", "huninn_tc_subset.ttf"),
    "zh_TW_noto": ("ZhHant",
                   "https://github.com/notofonts/noto-cjk/raw/main/Sans/SubsetOTF/TC/NotoSansTC-Bold.otf",
                   "NotoSansTC-Bold.otf", "noto_tc_subset.otf"),
    "th": ("Th",
           "https://github.com/notofonts/noto-fonts/raw/main/hinted/ttf/NotoSansThai/NotoSansThai-Bold.ttf",
           "NotoSansThai-Bold.ttf", "noto_th_subset.ttf"),
}

# 动态文本会用到这些，必须留
EXTRA = set(
    "0123456789"
    "abcdefghijklmnopqrstuvwxyz"
    "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    " .,:;!?'\"()[]{}<>/\\|-_+=*#%&@~^$"
    "·…—–×÷←→↑↓★☆♥"
)


def dict_block(src: str, name: str) -> str:
    i = src.index(f"Dictionary<string, string> {name} = new()")
    j = src.index("\n    };", i)
    return src[i:j]


def chars_for(name: str) -> str:
    src = open(os.path.join(ROOT, "scripts", "LocTable.cs"), encoding="utf-8").read()
    block = dict_block(src, name)
    vals = re.findall(r'=\s*"([^"]*)"', block)
    used = set("".join(vals)) | EXTRA
    return "".join(sorted(c for c in used if c.strip() or c == " "))


def fetch(url: str, dest: str) -> str:
    if os.path.exists(dest) and os.path.getsize(dest) > 10000:
        return dest
    os.makedirs(SRC_DIR, exist_ok=True)
    print(f"  downloading {os.path.basename(dest)} …")
    req = urllib.request.Request(url, headers={"User-Agent": "AntiSnake-font-subset"})
    with urllib.request.urlopen(req, timeout=180) as r, open(dest, "wb") as f:
        f.write(r.read())
    return dest


def main() -> int:
    ok = True
    for tag, (name, url, src_name, out_name) in JOBS.items():
        print(f"[{tag}]")
        try:
            src = fetch(url, os.path.join(SRC_DIR, src_name))
        except Exception as e:
            print("  download failed:", e)
            ok = False
            continue

        text = chars_for(name)
        text_file = os.path.join(SRC_DIR, f"{tag}_chars.txt")
        open(text_file, "w", encoding="utf-8").write(text)

        out = os.path.join(OUT_DIR, out_name)
        cmd = [
            PYFTSUBSET, src,
            f"--text-file={text_file}",
            f"--output-file={out}",
            "--layout-features=*",
            "--no-hinting",
            "--desubroutinize",
            "--drop-tables+=DSIG",
        ]
        r = subprocess.run(cmd, capture_output=True, text=True)
        if r.returncode != 0:
            print("  subset failed:", r.stderr[-400:])
            ok = False
            continue
        print(f"  {len(text)} chars -> {out_name}  {os.path.getsize(out)/1024:.1f} KB")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
