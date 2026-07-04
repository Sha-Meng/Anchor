using System;
using Anchor.ForceSystem;
using Anchor.LevelAnchorSystem;
using UnityEngine;

namespace Anchor.SystemValidation
{
    public enum ValidationHandSide
    {
        Left = 0,
        Right = 1
    }

    [Serializable]
    public struct ValidationHandInputState
    {
        public ValidationHandSide Side;
        public Vector2 ScreenPosition;
        public Vector3 WorldPosition;
        public Vector3 SurfaceNormal;
        public bool IsTouching;
        public bool HasValidWorldPosition;
        public GripQueryResult Grip;
        public AnchorPointQueryResult NearestAnchor;

        public void SetHit(Vector2 screenPosition, Vector3 worldPosition, Vector3 surfaceNormal)
        {
            ScreenPosition = screenPosition;
            WorldPosition = worldPosition;
            SurfaceNormal = surfaceNormal.sqrMagnitude > 0.0001f ? surfaceNormal.normalized : Vector3.back;
            IsTouching = true;
            HasValidWorldPosition = true;
        }

        public void Clear(Vector2 screenPosition)
        {
            ScreenPosition = screenPosition;
            IsTouching = false;
            HasValidWorldPosition = false;
            Grip = GripQueryResult.None();
            NearestAnchor = AnchorPointQueryResult.None(WorldPosition);
        }
    }
}
