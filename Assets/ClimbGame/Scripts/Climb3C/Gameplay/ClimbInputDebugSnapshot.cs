using UnityEngine;

namespace ClimbGame.Climb3C.Gameplay
{
    /// <summary>攀爬输入调试快照：供屏幕/场景可视化对比 raw→映射→实际手位。</summary>
    public readonly struct ClimbInputDebugSnapshot
    {
        public readonly bool Active;
        public readonly Vector2 RawScreenPos;
        public readonly Vector2 EffectiveScreenPos;
        public readonly Vector3 ReachStartTouchWorld;
        public readonly Vector3 ReachStartHandWorld;
        public readonly Vector3 MappedWorld;
        public readonly Vector3 CommandedWorld;
        public readonly Vector3 ActualHandWorld;

        public ClimbInputDebugSnapshot(
            bool active,
            Vector2 rawScreenPos,
            Vector2 effectiveScreenPos,
            Vector3 reachStartTouchWorld,
            Vector3 reachStartHandWorld,
            Vector3 mappedWorld,
            Vector3 commandedWorld,
            Vector3 actualHandWorld)
        {
            Active = active;
            RawScreenPos = rawScreenPos;
            EffectiveScreenPos = effectiveScreenPos;
            ReachStartTouchWorld = reachStartTouchWorld;
            ReachStartHandWorld = reachStartHandWorld;
            MappedWorld = mappedWorld;
            CommandedWorld = commandedWorld;
            ActualHandWorld = actualHandWorld;
        }

        public static ClimbInputDebugSnapshot Inactive => default;
    }
}
