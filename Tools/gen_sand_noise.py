"""生成用于 Custom/SandFall 的无缝可平铺分形噪声贴图。

- FFT 频谱法：结果天然无缝（周期性），无接缝。
- R / G / B 三通道各自独立的分形噪声，保证 shader 两层采样互不重复。
- 输出 1024x1024 PNG，灰度云状（雾感），对比度做了直方图拉伸。
"""

import numpy as np
from PIL import Image

SIZE = 1024


def tileable_fbm(size, beta, seed):
    """用 1/f^beta 频谱生成无缝分形噪声，返回 [0,1] 的二维数组。"""
    rng = np.random.default_rng(seed)

    # 频率坐标（含 fftshift 前的排布），构造径向频率
    fy = np.fft.fftfreq(size)
    fx = np.fft.fftfreq(size)
    fxx, fyy = np.meshgrid(fx, fy)
    radius = np.sqrt(fxx ** 2 + fyy ** 2)
    radius[0, 0] = 1.0  # 避免除零（直流分量）

    # 频谱幅度按 1/f^beta 衰减，随机相位保证是噪声
    amplitude = 1.0 / (radius ** beta)
    amplitude[0, 0] = 0.0  # 去掉直流，后面再归一化

    phase = rng.uniform(0, 2 * np.pi, (size, size))
    spectrum = amplitude * np.exp(1j * phase)

    # 逆变换得到实数场（周期性 => 无缝平铺）
    field = np.fft.ifft2(spectrum).real

    # 归一化 + 直方图百分位拉伸，得到饱满的云状对比
    lo, hi = np.percentile(field, 2), np.percentile(field, 98)
    field = np.clip((field - lo) / (hi - lo), 0.0, 1.0)
    return field


def main():
    # beta 越小 => 高频越多、结构越细。沙瀑要中频丝缕感，取 1.5~1.7。
    # R：主体基底层
    r = tileable_fbm(SIZE, beta=1.7, seed=101)
    # G：用于 UV 横向扰动，结构不同、更细
    g = tileable_fbm(SIZE, beta=1.55, seed=202)
    # B：备用第三层，更细
    b = tileable_fbm(SIZE, beta=1.45, seed=303)

    # 让 R 略偏亮一点，保证两次相乘后仍有足够浓度
    r = np.clip(r * 1.05, 0.0, 1.0)

    img = np.stack([r, g, b], axis=-1)
    img8 = (img * 255.0 + 0.5).astype(np.uint8)

    out = Image.fromarray(img8, mode="RGB")
    out.save(r"F:\Anchor\Assets\Art\liusha_noise.png")
    print("saved liusha_noise.png", out.size)


if __name__ == "__main__":
    main()
