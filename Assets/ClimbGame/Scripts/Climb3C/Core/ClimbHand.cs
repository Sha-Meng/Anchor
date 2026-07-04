namespace ClimbGame.Climb3C.Core
{
    /// <summary>当前攀爬手标识。</summary>
    public enum ClimbHand
    {
        None = 0,
        Left = 1,
        Right = 2
    }

    /// <summary>铆钉靠近度触觉分档。</summary>
    public enum HapticTier
    {
        None = 0,
        Slight = 1,
        Intense = 2
    }

    public static class ClimbHandExtensions
    {
        public static ClimbHand Other(this ClimbHand hand)
        {
            switch (hand)
            {
                case ClimbHand.Left: return ClimbHand.Right;
                case ClimbHand.Right: return ClimbHand.Left;
                default: return ClimbHand.None;
            }
        }

        /// <summary>屏幕水平比例 x(0..1) 落在哪一侧输入区（相对分割线）。</summary>
        public static ClimbHand SideFromScreenX(float normalizedX, float split)
        {
            return normalizedX < split ? ClimbHand.Left : ClimbHand.Right;
        }
    }
}
