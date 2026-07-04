using UnityEngine;

namespace ClimbGame.Climb3C.Input
{
    /// <summary>
    /// 胶囊体防穿模：把角色胶囊体与墙体做重叠检测，若发生穿插，沿碰撞法线方向把角色略微推出，
    /// 使胶囊体贴合墙面而不陷入墙体。基于 <see cref="Physics.ComputePenetration"/> 计算最小平移量（MTV），
    /// 多次迭代以处理同时贴多面墙的角落情形。方案简单直接、确定性强，不依赖物理步进。
    /// </summary>
    public sealed class CapsuleWallResolver
    {
        private readonly CapsuleCollider _capsule;
        private readonly LayerMask _wallMask;
        private readonly int _maxIterations;
        private readonly float _skin;
        private readonly Collider[] _hits = new Collider[16];

        /// <param name="capsule">角色的防穿模胶囊体（挂在化身根节点上）。</param>
        /// <param name="wallMask">参与穿模检测的墙体层。</param>
        /// <param name="maxIterations">去穿模迭代次数（角落多墙时需要多次）。</param>
        /// <param name="skin">推出后额外保留的贴合间隙，避免抖动/贴死。</param>
        public CapsuleWallResolver(CapsuleCollider capsule, LayerMask wallMask, int maxIterations = 4, float skin = 0.005f)
        {
            _capsule = capsule;
            _wallMask = wallMask;
            _maxIterations = Mathf.Max(1, maxIterations);
            _skin = Mathf.Max(0f, skin);
        }

        /// <summary>
        /// 给定期望的躯干中心（调用前胶囊体应已摆到该中心对应位姿），返回去穿模后的中心。
        /// 未发生穿插时原样返回。返回值 = 期望中心 + 世界空间修正位移。
        /// </summary>
        public Vector3 Resolve(Vector3 desiredCenter)
        {
            if (_capsule == null) return desiredCenter;

            Transform t = _capsule.transform;
            Quaternion rot = t.rotation;
            Transform root = t.root;
            Vector3 correction = Vector3.zero;

            for (int iter = 0; iter < _maxIterations; iter++)
            {
                ComputeCapsuleWorld(correction, out Vector3 p0, out Vector3 p1, out float radius);
                int count = Physics.OverlapCapsuleNonAlloc(p0, p1, radius, _hits, _wallMask, QueryTriggerInteraction.Ignore);

                bool pushed = false;
                for (int i = 0; i < count; i++)
                {
                    Collider other = _hits[i];
                    if (other == null || other == _capsule) continue;
                    // 排除角色自身各部件（同一根节点下），只对墙体去穿模
                    if (root != null && other.transform.IsChildOf(root)) continue;

                    if (Physics.ComputePenetration(
                            _capsule, t.position + correction, rot,
                            other, other.transform.position, other.transform.rotation,
                            out Vector3 dir, out float dist))
                    {
                        // 沿碰撞法线方向推出穿插深度，并留一点贴合间隙
                        correction += dir * (dist + _skin);
                        pushed = true;
                    }
                }

                if (!pushed) break;
            }

            return desiredCenter + correction;
        }

        /// <summary>按胶囊体朝向与世界缩放，计算加上 correction 后的两端球心与有效半径。</summary>
        private void ComputeCapsuleWorld(Vector3 correction, out Vector3 p0, out Vector3 p1, out float radius)
        {
            Transform t = _capsule.transform;
            Vector3 scale = t.lossyScale;
            float sx = Mathf.Abs(scale.x), sy = Mathf.Abs(scale.y), sz = Mathf.Abs(scale.z);

            Vector3 localAxis;
            float axisScale, radialScale;
            switch (_capsule.direction)
            {
                case 0: localAxis = Vector3.right;   axisScale = sx; radialScale = Mathf.Max(sy, sz); break;
                case 2: localAxis = Vector3.forward; axisScale = sz; radialScale = Mathf.Max(sx, sy); break;
                default: localAxis = Vector3.up;     axisScale = sy; radialScale = Mathf.Max(sx, sz); break;
            }

            radius = _capsule.radius * radialScale;
            float height = Mathf.Max(_capsule.height * axisScale, radius * 2f);
            float halfSeg = Mathf.Max(0f, height * 0.5f - radius);

            Vector3 centerWorld = t.TransformPoint(_capsule.center) + correction;
            Vector3 axis = t.rotation * localAxis;
            p0 = centerWorld + axis * halfSeg;
            p1 = centerWorld - axis * halfSeg;
        }
    }
}
