using UnityEngine;

namespace ClimbGame.Art
{
    /// <summary>
    /// Procedurally generates a pixel-art climbing animation for a small humanoid.
    /// Single responsibility: produce sprites. It has no dependency on input, movement
    /// or scene setup, so it can be used both at runtime and from an editor exporter.
    /// </summary>
    public static class ClimberSpriteFactory
    {
        public const int Width = 32;
        public const int Height = 40;

        // Palette (kept private so the visual identity lives in one place).
        private static readonly Color32 Outline = new Color32(26, 24, 38, 255);
        private static readonly Color32 Skin = new Color32(238, 196, 150, 255);
        private static readonly Color32 Hair = new Color32(92, 60, 38, 255);
        private static readonly Color32 Shirt = new Color32(60, 150, 220, 255);
        private static readonly Color32 ShirtDark = new Color32(40, 108, 170, 255);
        private static readonly Color32 Pants = new Color32(70, 72, 98, 255);
        private static readonly Color32 Shoe = new Color32(36, 34, 46, 255);

        public struct ClimberSprites
        {
            public Sprite[] ClimbFrames;
            public Sprite Idle;
        }

        /// <summary>Builds the full climbing cycle plus a resting/idle pose.</summary>
        public static ClimberSprites Create(int frameCount = 8, int pixelsPerUnit = 32)
        {
            frameCount = Mathf.Max(2, frameCount);
            var frames = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                float phase = i / (float)frameCount; // 0..1 climb cycle
                frames[i] = BuildFrame(phase, pixelsPerUnit);
            }

            return new ClimberSprites
            {
                ClimbFrames = frames,
                // Idle = both hands high, both feet planted: a natural "clinging" pose.
                Idle = BuildFrame(0f, pixelsPerUnit)
            };
        }

        private static Sprite BuildFrame(float phase, int pixelsPerUnit)
        {
            var buffer = TextureUtil.NewCanvas(Width, Height);

            // Diagonal climbing gait: left arm rises with the right leg, and vice-versa.
            float swing = Mathf.Sin(phase * Mathf.PI * 2f);

            float centerX = 16f;

            // Anchors.
            Vector2 leftShoulder = new Vector2(centerX - 4f, 27f);
            Vector2 rightShoulder = new Vector2(centerX + 4f, 27f);
            Vector2 leftHip = new Vector2(centerX - 3f, 15f);
            Vector2 rightHip = new Vector2(centerX + 3f, 15f);

            // Hands reach up alternately (high grip pose).
            Vector2 leftHand = new Vector2(centerX - 8f - 0.5f * swing, 33f + 3.5f * swing);
            Vector2 rightHand = new Vector2(centerX + 8f + 0.5f * swing, 33f - 3.5f * swing);
            Vector2 leftElbow = Vector2.Lerp(leftShoulder, leftHand, 0.5f) + new Vector2(-2.5f, 0f);
            Vector2 rightElbow = Vector2.Lerp(rightShoulder, rightHand, 0.5f) + new Vector2(2.5f, 0f);

            // Feet push down opposite to the same-side hand (diagonal gait).
            Vector2 leftFoot = new Vector2(centerX - 5f, 6f - 3f * swing);
            Vector2 rightFoot = new Vector2(centerX + 5f, 6f + 3f * swing);
            Vector2 leftKnee = Vector2.Lerp(leftHip, leftFoot, 0.5f) + new Vector2(-2f, 0f);
            Vector2 rightKnee = Vector2.Lerp(rightHip, rightFoot, 0.5f) + new Vector2(2f, 0f);

            // --- Legs (drawn first so the torso overlaps their roots) ---
            DrawLimb(buffer, leftHip, leftKnee, leftFoot, Pants, Shoe);
            DrawLimb(buffer, rightHip, rightKnee, rightFoot, Pants, Shoe);

            // --- Torso ---
            TextureUtil.FillRect(buffer, Width, Height, 12, 14, 20, 27, Shirt);
            // A little shading down the centre for depth.
            TextureUtil.FillRect(buffer, Width, Height, 15, 14, 16, 27, ShirtDark);

            // --- Arms (over the torso) ---
            DrawLimb(buffer, leftShoulder, leftElbow, leftHand, Shirt, Skin);
            DrawLimb(buffer, rightShoulder, rightElbow, rightHand, Shirt, Skin);

            // --- Head + hair ---
            TextureUtil.DrawDisc(buffer, Width, Height, centerX, 31f, 4f, Skin);
            TextureUtil.FillRect(buffer, Width, Height, 12, 32, 20, 36, Hair);
            // Re-carve the face by redrawing the lower half of the head as skin.
            for (int y = 27; y <= 31; y++)
                for (int x = 12; x <= 20; x++)
                {
                    float dx = x - centerX, dy = y - 31f;
                    if (dx * dx + dy * dy <= 16f)
                        TextureUtil.SetPixel(buffer, Width, Height, x, y, Skin);
                }

            TextureUtil.AddOutline(buffer, Width, Height, Outline);

            return TextureUtil.CreateSprite(buffer, Width, Height, pixelsPerUnit, new Vector2(0.5f, 0.5f));
        }

        /// <summary>Draws a two-segment limb (shoulder/hip -> joint -> extremity) with an end cap.</summary>
        private static void DrawLimb(Color32[] buffer, Vector2 root, Vector2 joint, Vector2 tip, Color32 limbColor, Color32 capColor)
        {
            TextureUtil.DrawThickLine(buffer, Width, Height, root, joint, 3f, limbColor);
            TextureUtil.DrawThickLine(buffer, Width, Height, joint, tip, 3f, limbColor);
            TextureUtil.DrawDisc(buffer, Width, Height, tip.x, tip.y, 1.8f, capColor);
        }
    }
}
