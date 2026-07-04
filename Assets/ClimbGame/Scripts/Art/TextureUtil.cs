using UnityEngine;

namespace ClimbGame.Art
{
    /// <summary>
    /// Small, self-contained helper for drawing pixel art into a Color32 buffer and
    /// turning it into a point-filtered <see cref="Sprite"/>.
    /// Pure utility: it knows nothing about the game, so it can be reused freely.
    /// Coordinate convention matches <see cref="Texture2D"/>: (0,0) is bottom-left, y grows upward.
    /// </summary>
    public static class TextureUtil
    {
        public static Color32[] NewCanvas(int width, int height)
        {
            var buffer = new Color32[width * height];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < buffer.Length; i++) buffer[i] = clear;
            return buffer;
        }

        public static void SetPixel(Color32[] buffer, int width, int height, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;
            buffer[y * width + x] = color;
        }

        public static void FillRect(Color32[] buffer, int width, int height, int x0, int y0, int x1, int y1, Color32 color)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            if (y0 > y1) (y0, y1) = (y1, y0);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    SetPixel(buffer, width, height, x, y, color);
        }

        public static void DrawDisc(Color32[] buffer, int width, int height, float cx, float cy, float radius, Color32 color)
        {
            int minX = Mathf.FloorToInt(cx - radius);
            int maxX = Mathf.CeilToInt(cx + radius);
            int minY = Mathf.FloorToInt(cy - radius);
            int maxY = Mathf.CeilToInt(cy + radius);
            float r2 = radius * radius;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    if (dx * dx + dy * dy <= r2)
                        SetPixel(buffer, width, height, x, y, color);
                }
            }
        }

        /// <summary>Draws a rounded line by sweeping a disc from a to b.</summary>
        public static void DrawThickLine(Color32[] buffer, int width, int height, Vector2 a, Vector2 b, float thickness, Color32 color)
        {
            float radius = Mathf.Max(0.5f, thickness * 0.5f);
            float distance = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                DrawDisc(buffer, width, height, p.x, p.y, radius, color);
            }
        }

        /// <summary>
        /// Adds a 1px outline of <paramref name="outline"/> around every opaque cluster
        /// by tinting transparent pixels that touch an opaque neighbour (4-connectivity).
        /// </summary>
        public static void AddOutline(Color32[] buffer, int width, int height, Color32 outline)
        {
            var result = (Color32[])buffer.Clone();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (buffer[y * width + x].a != 0) continue;
                    if (HasOpaqueNeighbour(buffer, width, height, x, y))
                        result[y * width + x] = outline;
                }
            }
            System.Array.Copy(result, buffer, buffer.Length);
        }

        private static bool HasOpaqueNeighbour(Color32[] buffer, int width, int height, int x, int y)
        {
            return IsOpaque(buffer, width, height, x + 1, y)
                || IsOpaque(buffer, width, height, x - 1, y)
                || IsOpaque(buffer, width, height, x, y + 1)
                || IsOpaque(buffer, width, height, x, y - 1);
        }

        private static bool IsOpaque(Color32[] buffer, int width, int height, int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return false;
            return buffer[y * width + x].a != 0;
        }

        public static Texture2D CreateTexture(Color32[] buffer, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "ClimbGame_Generated"
            };
            texture.SetPixels32(buffer);
            texture.Apply();
            return texture;
        }

        public static Sprite CreateSprite(Color32[] buffer, int width, int height, int pixelsPerUnit, Vector2 pivot)
        {
            var texture = CreateTexture(buffer, width, height);
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            return sprite;
        }
    }
}
