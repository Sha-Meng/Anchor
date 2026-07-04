using UnityEngine;

namespace ClimbGame.Climb3C.Input
{
    /// <summary>
    /// 墙面深度探针：在手的 (x, y) 处沿世界 +Z 轴打射线命中墙体，
    /// 把手的 Z 贴到命中的墙面表面（略微前置），使手在起伏的 3D 岩壁上"贴着墙面"移动。
    /// </summary>
    public sealed class WallDepthProbe
    {
        private readonly LayerMask _wallMask;
        private readonly float _castFromZ;
        private readonly float _castDistance;
        private readonly float _surfaceOffset;

        public WallDepthProbe(LayerMask wallMask, float castFromZ, float castDistance, float surfaceOffset)
        {
            _wallMask = wallMask;
            _castFromZ = castFromZ;
            _castDistance = Mathf.Max(0.01f, castDistance);
            _surfaceOffset = surfaceOffset;
        }

        /// <summary>
        /// 把 target 的 z 贴到 (target.x, target.y) 处的墙面表面（沿 +Z 打射线）。
        /// 命中则 hit=true 并更新 z；未命中保持原 z。
        /// </summary>
        public Vector3 StickToWall(Vector3 target, out bool hit)
        {
            var origin = new Vector3(target.x, target.y, _castFromZ);
            if (Physics.Raycast(origin, Vector3.forward, out RaycastHit h, _castDistance, _wallMask, QueryTriggerInteraction.Ignore))
            {
                hit = true;
                // 手略微贴在表面前方（朝相机/-Z），避免陷入墙体
                target.z = h.point.z - _surfaceOffset;
            }
            else
            {
                hit = false;
            }
            return target;
        }
    }
}
