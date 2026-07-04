using UnityEngine;

namespace ClimbGame.Climb3C.Input
{
    /// <summary>
    /// 把屏幕触点映射到 3D 墙面表面：优先用相机射线与墙面碰撞体求交；
    /// 未命中时退化为与一个参考平面求交，保证总能得到一个墙面目标点。
    /// </summary>
    public sealed class WallProjector
    {
        private readonly Camera _camera;
        private readonly LayerMask _wallMask;
        private readonly float _planeZ;

        public WallProjector(Camera camera, LayerMask wallMask, float fallbackPlaneZ)
        {
            _camera = camera;
            _wallMask = wallMask;
            _planeZ = fallbackPlaneZ;
        }

        /// <summary>返回墙面上的世界目标点；out 为是否命中真实墙面碰撞体。</summary>
        public Vector3 Project(Vector2 screenPos, out bool hitWall)
        {
            hitWall = false;
            if (_camera == null) return Vector3.zero;

            Ray ray = _camera.ScreenPointToRay(screenPos);
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, _wallMask, QueryTriggerInteraction.Collide))
            {
                hitWall = true;
                return hit.point;
            }

            // 退化：与 z = planeZ 的平面求交
            var plane = new Plane(Vector3.back, new Vector3(0f, 0f, _planeZ));
            if (plane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }

            return ray.GetPoint(10f);
        }
    }
}
