"""离线预览 Custom/SandFall（4层柔和版）效果，复刻 frag 逻辑用于调参。

用法: python preview_sandfall.py [time] [speed] [density] [contrast] [flow] [tileX] [tileY]
其它参数(softness/bottom/edge/wind)在下方常量里。
"""

import sys
import numpy as np
from PIL import Image

NOISE = r"F:\Anchor\Assets\Art\liusha_noise.png"
OUT = r"F:\Anchor\Tools\sandfall_preview.png"
W = H = 512

COLOR = np.array([0.90, 0.82, 0.58])   # _Color 浅
COLOR2 = np.array([0.62, 0.50, 0.30])  # _Color2 深
BG = np.array([0.05, 0.06, 0.09])

SOFTNESS = 0.25     # 顶部渐入
BOTTOMFADE = 0.5    # 底部消散
EDGE = 0.5          # 左右羽化
WIND = 0.02


def load_noise():
    return np.asarray(Image.open(NOISE).convert("RGB"), dtype=np.float32) / 255.0


def sample(noise, u, v, ch):
    h, w, _ = noise.shape
    x = (u % 1.0) * w - 0.5
    y = ((1.0 - (v % 1.0)) % 1.0) * h - 0.5
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int)
    x1 = x0 + 1; y1 = y0 + 1
    fx = (x - x0); fy = (y - y0)
    x0 %= w; x1 %= w; y0 %= h; y1 %= h
    c00 = noise[y0, x0, ch]; c10 = noise[y0, x1, ch]
    c01 = noise[y1, x0, ch]; c11 = noise[y1, x1, ch]
    top = c00 * (1 - fx) + c10 * fx
    bot = c01 * (1 - fx) + c11 * fx
    return top * (1 - fy) + bot * fy


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def main():
    args = sys.argv[1:]
    g = lambda i, d: float(args[i]) if len(args) > i else d
    time = g(0, 3.0); speed = g(1, 0.35); density = g(2, 1.7)
    contrast = g(3, 1.4); flow = g(4, 0.06); tx = g(5, 2.0); ty = g(6, 1.5)

    noise = load_noise()
    px = (np.arange(W) + 0.5) / W
    py = (np.arange(H) + 0.5) / H
    U, V = np.meshgrid(px, py)  # (H,W) V向上

    t = time * speed
    wind = np.sin(time * 0.6) * WIND
    u = U * tx; v = V * ty

    l1 = sample(noise, u * 1.0 + wind,       v * 1.0 - t * 1.00, 0)
    l2 = sample(noise, u * 2.0 - wind * 0.7, v * 2.0 - t * 1.70, 1)
    l3 = sample(noise, u * 4.0 + wind * 0.5, v * 4.0 - t * 2.60, 2)
    u2 = u + (l2 - 0.5) * flow
    l4 = sample(noise, u2 * 3.0,             v * 3.0 - t * 2.20, 0)

    n = (l1 * 0.50 + l2 * 0.28 + l3 * 0.14 + l4 * 0.24) / 1.16
    sand = np.clip(n * density, 0, 1)
    sand = smoothstep(0.5 - 0.5 * contrast, 0.5 + 0.5 * contrast, sand)

    fade_top = smoothstep(1.0, 1.0 - SOFTNESS, V)
    fade_bottom = smoothstep(0.0, BOTTOMFADE, V)
    edge = smoothstep(0.0, EDGE, 1.0 - np.abs(U - 0.5) * 2.0)

    alpha = (sand * fade_top * fade_bottom * edge)[..., None]
    col = COLOR2 * (1 - sand[..., None]) + COLOR * sand[..., None]
    rgb = col * alpha + BG * (1 - alpha)

    out = (np.clip(rgb, 0, 1) * 255 + 0.5).astype(np.uint8)
    Image.fromarray(out, "RGB").save(OUT)
    print("saved t=%.2f speed=%.2f density=%.2f contrast=%.2f flow=%.3f tile=(%.1f,%.1f)"
          % (time, speed, density, contrast, flow, tx, ty))


if __name__ == "__main__":
    main()
