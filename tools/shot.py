"""截取游戏窗口（验收用）。

用法:
    python tools/shot.py <输出png> [游戏用户参数...]

会临时把 user://settings.cfg 的语言设为 en，启动游戏、截图、退出，之后还原。
"""

import ctypes
import ctypes.wintypes as wt
import os
import subprocess
import sys
import time

from PIL import ImageGrab

GODOT = r"D:\Program Files (x86)\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TITLE = "贪吃蛇减肥"


def dpi_aware():
    try:
        ctypes.windll.shcore.SetProcessDpiAwareness(2)
    except Exception:
        ctypes.windll.user32.SetProcessDPIAware()


def find_window(title, timeout=25.0):
    user32 = ctypes.windll.user32
    end = time.time() + timeout
    while time.time() < end:
        found = []

        @ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM)
        def cb(hwnd, _):
            if not user32.IsWindowVisible(hwnd):
                return True
            n = user32.GetWindowTextLengthW(hwnd)
            if n == 0:
                return True
            buf = ctypes.create_unicode_buffer(n + 1)
            user32.GetWindowTextW(hwnd, buf, n + 1)
            if title in buf.value:
                found.append(hwnd)
                return False
            return True

        user32.EnumWindows(cb, 0)
        if found:
            return found[0]
        time.sleep(0.3)
    return None


def main():
    out = sys.argv[1]
    extra = sys.argv[2:]

    user_dir = os.path.join(os.environ["APPDATA"], "Godot", "app_userdata", TITLE)
    os.makedirs(user_dir, exist_ok=True)
    cfg = os.path.join(user_dir, "settings.cfg")
    backup = None
    if os.path.exists(cfg):
        backup = open(cfg, "rb").read()
    with open(cfg, "w", encoding="utf-8") as f:
        f.write('[ui]\nlanguage="en"\n')

    dpi_aware()
    cmd = [GODOT, "--path", ROOT]
    if extra:
        cmd += ["--"] + extra
    proc = subprocess.Popen(cmd)

    try:
        hwnd = find_window(TITLE)
        if not hwnd:
            print("window not found")
            return 1
        time.sleep(1.4)                      # 等入场动画

        # 置顶 + 前置，否则截到的是压在它上面的窗口
        user32 = ctypes.windll.user32
        HWND_TOPMOST, HWND_NOTOPMOST = -1, -2
        SWP_NOMOVE, SWP_NOSIZE = 0x0002, 0x0001
        user32.ShowWindow(hwnd, 9)           # SW_RESTORE
        user32.SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE)
        user32.SetForegroundWindow(hwnd)
        time.sleep(1.0)

        rect = wt.RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(rect))
        box = (rect.left, rect.top, rect.right, rect.bottom)
        ImageGrab.grab(bbox=box, all_screens=True).save(out)
        print("saved", out, box)
        user32.SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE)
    finally:
        proc.terminate()
        try:
            proc.wait(timeout=6)
        except Exception:
            proc.kill()
        if backup is not None:
            open(cfg, "wb").write(backup)
        else:
            os.remove(cfg)
    return 0


if __name__ == "__main__":
    sys.exit(main())
