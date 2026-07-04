using System.Collections.Generic;
using Anchor.ForceSystem;
using Anchor.LevelAnchorSystem;
using DesignerSpace;
using UnityEngine;

namespace Anchor.SystemValidation
{
    [DisallowMultipleComponent]
    public sealed class SystemValidationLevelAnchorRegistry : MonoBehaviour, ILevelAnchorQuery, IGripQueryProvider
    {
        [SerializeField] private bool rebuildOnAwake = true;
        [SerializeField] private Transform planeReference;
        [SerializeField] private LevelAnchorQuerySettings querySettings = LevelAnchorQuerySettings.CreateDefault();
        [SerializeField] private bool logInitialization;
        [SerializeField] private bool logGripQueries = true;
        [SerializeField] private bool drawGizmos = true;

        private readonly LevelAnchorMap _map = new LevelAnchorMap();
        private readonly List<string> _initializationWarnings = new List<string>();

        public int RegisteredCount => _map.Count;
        public int SkippedCount => _initializationWarnings.Count;
        public IReadOnlyList<string> InitializationWarnings => _initializationWarnings;
        public LevelAnchorMap Map => _map;

        private void Start()
        {
            if (rebuildOnAwake)
            {
                RebuildFromScene();
            }
        }

        private void OnValidate()
        {
            querySettings = querySettings.Sanitized();
        }

        public void RebuildFromScene()
        {
            Rebuild(FindObjectsOfType<AnchorPoint>());
        }

        public void Rebuild(IList<AnchorPoint> anchors)
        {
            _initializationWarnings.Clear();
            _map.Rebuild(anchors, planeReference, querySettings, _initializationWarnings);

            if (!logInitialization)
            {
                return;
            }

            Debug.Log(
                $"SystemValidationLevelAnchorRegistry initialized: registered={RegisteredCount}, skipped={SkippedCount}, cellSize={_map.CellSize:0.###}",
                this);

            for (int i = 0; i < _initializationWarnings.Count; i++)
            {
                Debug.LogWarning(_initializationWarnings[i], this);
            }
        }

        public int GetStability(Vector3 worldPosition)
        {
            return _map.GetStability(worldPosition);
        }

        public bool TryFindNearestAnchor(
            Vector3 worldPosition,
            out AnchorPointQueryResult result,
            float maxDistance = float.PositiveInfinity)
        {
            return _map.TryFindNearestAnchor(worldPosition, out result, maxDistance);
        }

        public bool TryQueryGrip(Vector3 handPosition, float radius, out GripQueryResult result)
        {
            // UnityEngine.Debug.Log($"TryQueryGrip: handPosition={handPosition}, radius={radius}");
            // 手部攀附判定只看 xy 平面投影距离，忽略 z（深度）差异：
            // 攀爬墙面凹凸导致锚点 z 各异，用 xy 投影判定更符合"屏幕上手贴到抓点"的直觉。
            var anchors = _map.Anchors;
            int bestIndex = -1;
            float bestSqrXY = float.PositiveInfinity;
            for (int i = 0; i < anchors.Count; i++)
            {
                var a = anchors[i];
                if (!a.IsValid)
                {
                    continue;
                }

                float dx = handPosition.x - a.WorldPosition.x;
                float dy = handPosition.y - a.WorldPosition.y;
                float sqr = dx * dx + dy * dy;
                if (sqr < bestSqrXY)
                {
                    bestSqrXY = sqr;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                if (logGripQueries)
                {
                    Debug.Log($"[GripQuery] hand={handPosition} radius={radius:0.###} → 无任何锚点，判定失败", this);
                }
                result = GripQueryResult.None();
                return false;
            }

            var best = anchors[bestIndex];
            float distanceXY = Mathf.Sqrt(bestSqrXY);
            float zDiff = handPosition.z - best.WorldPosition.z;
            // 有效抓取半径取查询半径与锚点自带 GrabRadius 的较大者：
            // 既让"在查询半径内"就能形成候选，也让抓握质量在该范围内平滑（小距离→高质量）。
            float effectiveRadius = Mathf.Max(radius, best.GrabRadius);
            int stability = CalculateStabilityXY(best, distanceXY, effectiveRadius);
            if (stability <= 0)
            {
                if (logGripQueries)
                {
                    Debug.Log(
                        $"[GripQuery] 失败：hand={handPosition} 最近锚点='{best.DebugName}'({best.WorldPosition}) " +
                        $"xy距离={distanceXY:0.###} z差={zDiff:0.###} grabRadius={best.GrabRadius:0.###} stability={stability}",
                        this);
                }
                result = GripQueryResult.None();
                return false;
            }

            float gripQuality01 = Mathf.Clamp01(stability / 10f);
            if (logGripQueries)
            {
                Debug.Log(
                    $"[GripQuery] 成功：hand={handPosition} 抓点='{best.DebugName}'({best.WorldPosition}) " +
                    $"xy距离={distanceXY:0.###} z差={zDiff:0.###}（已忽略）grabRadius={best.GrabRadius:0.###} " +
                    $"stability={stability} quality={gripQuality01:0.##}",
                    this);
            }
            result = GripQueryResult.Candidate(
                ForcePointType.ValidHold,
                gripQuality01,
                false,
                default,
                best.Id.ToString(),
                best.DebugName);
            return true;
        }

        /// <summary>与 LevelAnchorMap 一致的稳定度公式，但距离用 xy 投影距离、半径用有效抓取半径。</summary>
        private static int CalculateStabilityXY(AnchorPointData data, float distance, float radius)
        {
            if (!data.IsValid || radius <= 0f || distance > radius)
            {
                return 0;
            }

            float normalized = Mathf.Clamp01(distance / radius);
            int stability = Mathf.CeilToInt(data.BaseStability * (1f - normalized));
            return Mathf.Clamp(stability, 1, data.BaseStability);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || _map == null)
            {
                return;
            }

            var anchors = _map.Anchors;
            for (int i = 0; i < anchors.Count; i++)
            {
                var anchor = anchors[i];
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
                Gizmos.DrawWireSphere(anchor.WorldPosition, anchor.GrabRadius);
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(anchor.WorldPosition, 0.08f);
            }
        }
    }
}
