using System.Collections.Generic;
using UnityEngine;

namespace ClimbGame.Climb3C.Core
{
    /// <summary>
    /// 管理场景中的所有铆钉，提供"给定手末端求最近铆钉与距离"的查询。
    /// </summary>
    public sealed class RivetField : MonoBehaviour
    {
        private readonly List<RivetPoint> _rivets = new List<RivetPoint>();

        public IReadOnlyList<RivetPoint> Rivets => _rivets;

        /// <summary>铆钉在场内的索引（供联机同步引用）；不存在返回 -1。</summary>
        public int IndexOf(RivetPoint rivet) => rivet == null ? -1 : _rivets.IndexOf(rivet);

        /// <summary>按索引取铆钉（联机同步时用）；越界返回 null。</summary>
        public RivetPoint GetByIndex(int index) =>
            index >= 0 && index < _rivets.Count ? _rivets[index] : null;

        public void Register(RivetPoint rivet)
        {
            if (rivet != null && !_rivets.Contains(rivet))
            {
                _rivets.Add(rivet);
            }
        }

        public void RefreshFromScene()
        {
            _rivets.Clear();
            _rivets.AddRange(FindObjectsOfType<RivetPoint>());
        }

        private void Awake()
        {
            if (_rivets.Count == 0)
            {
                RefreshFromScene();
            }
        }

        /// <summary>返回距 worldPos 最近的铆钉；找不到返回 null。out 距离为世界距离。</summary>
        public RivetPoint FindNearest(Vector3 worldPos, out float distance)
        {
            return FindNearestExcluding(worldPos, null, out distance);
        }

        /// <summary>返回距 worldPos 最近、且不等于 exclude 的铆钉（用于排除当前手正抓着的那颗）。</summary>
        public RivetPoint FindNearestExcluding(Vector3 worldPos, RivetPoint exclude, out float distance)
        {
            RivetPoint best = null;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < _rivets.Count; i++)
            {
                RivetPoint r = _rivets[i];
                if (r == null || r == exclude) continue;
                float sqr = (r.GrabPosition - worldPos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = r;
                }
            }

            distance = best != null ? Mathf.Sqrt(bestSqr) : float.PositiveInfinity;
            return best;
        }

        /// <summary>在 y 值低于 referenceY 的铆钉中找最近的（用于摔落后向下吸附）。</summary>
        public RivetPoint FindNearestBelow(Vector3 worldPos, float referenceY, out float distance)
        {
            RivetPoint best = null;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < _rivets.Count; i++)
            {
                RivetPoint r = _rivets[i];
                if (r == null) continue;
                if (r.GrabPosition.y > referenceY) continue;
                float sqr = (r.GrabPosition - worldPos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = r;
                }
            }

            distance = best != null ? Mathf.Sqrt(bestSqr) : float.PositiveInfinity;
            return best;
        }
    }
}
