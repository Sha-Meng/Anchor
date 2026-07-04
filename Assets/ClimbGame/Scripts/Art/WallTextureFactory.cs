using UnityEngine;

namespace ClimbGame.Art
{
    /// <summary>
    /// Generates a simple pixel-art rock wall used as the climbing surface backdrop.
    /// Purely decorative and dependency-free.
    /// </summary>
    public static class WallTextureFactory
    {
        public static Sprite Create(int width = 96, int height = 128, int pixelsPerUnit = 32, int seed = 1337)
        {
            var random = new System.Random(seed);
            var buffer = TextureUtil.NewCanvas(width, height);

            var baseColor = new Color32(78, 84, 96, 255);
            var mortar = new Color32(52, 56, 66, 255);
            var holdColor = new Color32(212, 152, 84, 255);

            // Base fill with subtle per-pixel noise for a rocky feel.
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int n = random.Next(-8, 9);
                    var c = new Color32(
                        (byte)Mathf.Clamp(baseColor.r + n, 0, 255),
                        (byte)Mathf.Clamp(baseColor.g + n, 0, 255),
                        (byte)Mathf.Clamp(baseColor.b + n, 0, 255),
                        255);
                    TextureUtil.SetPixel(buffer, width, height, x, y, c);
                }
            }

            // Offset-brick mortar lines.
            const int brickH = 16;
            const int brickW = 24;
            for (int y = 0; y < height; y += brickH)
            {
                for (int x = 0; x < width; x++)
                    TextureUtil.SetPixel(buffer, width, height, x, y, mortar);

                int rowIndex = y / brickH;
                int offset = (rowIndex % 2) * (brickW / 2);
                for (int x = offset; x < width; x += brickW)
                    for (int yy = y; yy < y + brickH && yy < height; yy++)
                        TextureUtil.SetPixel(buffer, width, height, x, yy, mortar);
            }

            // Scatter a few climbing "holds" so the surface reads as climbable.
            int holds = Mathf.Max(6, (width * height) / 900);
            for (int i = 0; i < holds; i++)
            {
                int hx = random.Next(4, width - 4);
                int hy = random.Next(4, height - 4);
                TextureUtil.DrawDisc(buffer, width, height, hx, hy, random.Next(2, 4), holdColor);
            }

            return TextureUtil.CreateSprite(buffer, width, height, pixelsPerUnit, new Vector2(0.5f, 0.5f));
        }
    }
}
