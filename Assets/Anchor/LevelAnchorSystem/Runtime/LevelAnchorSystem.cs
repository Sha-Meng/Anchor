using System;
using System.Collections.Generic;
using Anchor.ForceSystem;
using DesignerSpace;
using UnityEngine;

namespace Anchor.LevelAnchorSystem
{
    [Serializable]
    public struct LevelAnchorQuerySettings
    {
        [Tooltip("平面 cell 尺寸。<= 0 时使用最大抓取半径。")]
        public float CellSize;

        [Tooltip("在半径覆盖范围之外额外扩张的 cell 圈数，用于容纳起伏岩壁的投影误差。")]
        public int ExtraNeighborCells;

        public static LevelAnchorQuerySettings CreateDefault()
        {
            return new LevelAnchorQuerySettings
            {
                CellSize = 0f,
                ExtraNeighborCells = 1
            };
        }

        public LevelAnchorQuerySettings Sanitized()
        {
            return new LevelAnchorQuerySettings
            {
                CellSize = Mathf.Max(0f, CellSize),
                ExtraNeighborCells = Mathf.Max(0, ExtraNeighborCells)
            };
        }
    }

    [Serializable]
    public struct AnchorPointData
    {
        public int Id;
        public string DebugName;
        public Vector3 WorldPosition;
        public int BaseStability;
        public float GrabRadius;

        public bool IsValid => Id > 0 && BaseStability > 0 && GrabRadius > 0f;
    }

    [Serializable]
    public struct AnchorPointQueryResult
    {
        public bool Found;
        public int Id;
        public string DebugName;
        public Vector3 WorldPosition;
        public float Distance;
        public int BaseStability;
        public int CurrentStability;
        public float GrabRadius;

        public float GripQuality01 => Mathf.Clamp01(CurrentStability / 10f);

        public static AnchorPointQueryResult None(Vector3 queryPosition)
        {
            return new AnchorPointQueryResult
            {
                Found = false,
                Id = 0,
                DebugName = "None",
                WorldPosition = queryPosition,
                Distance = Mathf.Infinity,
                BaseStability = 0,
                CurrentStability = 0,
                GrabRadius = 0f
            };
        }
    }

    public interface ILevelAnchorQuery
    {
        int GetStability(Vector3 worldPosition);

        bool TryFindNearestAnchor(
            Vector3 worldPosition,
            out AnchorPointQueryResult result,
            float maxDistance = float.PositiveInfinity);
    }

    public sealed class LevelAnchorMap : ILevelAnchorQuery
    {
        private readonly List<AnchorPointData> _anchors = new List<AnchorPointData>();
        private readonly Dictionary<Vector2Int, List<int>> _cells = new Dictionary<Vector2Int, List<int>>();

        private Transform _planeReference;
        private float _cellSize = 1f;
        private float _maxGrabRadius;
        private int _queryCellRange = 1;
        private Vector2Int _minCell;
        private Vector2Int _maxCell;
        private bool _hasCellBounds;

        public int Count => _anchors.Count;
        public float CellSize => _cellSize;
        public float MaxGrabRadius => _maxGrabRadius;
        public int LastVisitedCellCount { get; private set; }
        public int LastVisitedCandidateCount { get; private set; }

        public IReadOnlyList<AnchorPointData> Anchors => _anchors;

        public void Rebuild(
            IList<AnchorPoint> anchorPoints,
            Transform planeReference,
            LevelAnchorQuerySettings settings,
            IList<string> warnings)
        {
            _anchors.Clear();
            _cells.Clear();
            _planeReference = planeReference;
            _maxGrabRadius = 0f;
            LastVisitedCellCount = 0;
            LastVisitedCandidateCount = 0;
            _hasCellBounds = false;

            settings = settings.Sanitized();
            var nextId = 1;

            if (anchorPoints != null)
            {
                for (int i = 0; i < anchorPoints.Count; i++)
                {
                    var anchorPoint = anchorPoints[i];
                    if (!TryCreateData(anchorPoint, nextId, warnings, out var data))
                    {
                        continue;
                    }

                    _anchors.Add(data);
                    _maxGrabRadius = Mathf.Max(_maxGrabRadius, data.GrabRadius);
                    nextId++;
                }
            }

            _cellSize = settings.CellSize > 0f ? settings.CellSize : Mathf.Max(0.01f, _maxGrabRadius);
            _queryCellRange = Mathf.CeilToInt(_maxGrabRadius / _cellSize) + settings.ExtraNeighborCells;
            _queryCellRange = Mathf.Max(1, _queryCellRange);

            for (int i = 0; i < _anchors.Count; i++)
            {
                AddToCell(i);
            }
        }

        public int GetStability(Vector3 worldPosition)
        {
            return TryFindBestStableAnchor(worldPosition, out var result)
                ? result.CurrentStability
                : 0;
        }

        public bool TryFindNearestAnchor(
            Vector3 worldPosition,
            out AnchorPointQueryResult result,
            float maxDistance = float.PositiveInfinity)
        {
            result = AnchorPointQueryResult.None(worldPosition);
            LastVisitedCellCount = 0;
            LastVisitedCandidateCount = 0;

            if (_anchors.Count == 0)
            {
                return false;
            }

            var queryCell = ProjectToCell(worldPosition);
            var sanitizedMaxDistance = maxDistance > 0f ? maxDistance : 0f;
            var maxRing = float.IsInfinity(sanitizedMaxDistance)
                ? CalculateMaxRingToBounds(queryCell)
                : Mathf.CeilToInt(sanitizedMaxDistance / _cellSize) + _queryCellRange;

            var bestIndex = -1;
            var bestDistanceSqr = Mathf.Infinity;
            var maxDistanceSqr = float.IsInfinity(sanitizedMaxDistance)
                ? Mathf.Infinity
                : sanitizedMaxDistance * sanitizedMaxDistance;

            for (int ring = 0; ring <= maxRing; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    for (int dy = -ring; dy <= ring; dy++)
                    {
                        if (ring > 0 && Mathf.Abs(dx) != ring && Mathf.Abs(dy) != ring)
                        {
                            continue;
                        }

                        var cell = new Vector2Int(queryCell.x + dx, queryCell.y + dy);
                        if (!_cells.TryGetValue(cell, out var indices))
                        {
                            continue;
                        }

                        LastVisitedCellCount++;

                        for (int i = 0; i < indices.Count; i++)
                        {
                            LastVisitedCandidateCount++;
                            var index = indices[i];
                            var data = _anchors[index];
                            var distanceSqr = (worldPosition - data.WorldPosition).sqrMagnitude;
                            if (distanceSqr > maxDistanceSqr || distanceSqr >= bestDistanceSqr)
                            {
                                continue;
                            }

                            bestIndex = index;
                            bestDistanceSqr = distanceSqr;
                        }
                    }
                }
            }

            if (bestIndex < 0)
            {
                return false;
            }

            result = BuildResult(_anchors[bestIndex], worldPosition, Mathf.Sqrt(bestDistanceSqr));
            return true;
        }

        private bool TryFindBestStableAnchor(Vector3 worldPosition, out AnchorPointQueryResult result)
        {
            result = AnchorPointQueryResult.None(worldPosition);
            LastVisitedCellCount = 0;
            LastVisitedCandidateCount = 0;

            if (_anchors.Count == 0)
            {
                return false;
            }

            var queryCell = ProjectToCell(worldPosition);
            var bestStability = 0;
            var bestDistanceSqr = Mathf.Infinity;
            var bestIndex = -1;

            for (int dx = -_queryCellRange; dx <= _queryCellRange; dx++)
            {
                for (int dy = -_queryCellRange; dy <= _queryCellRange; dy++)
                {
                    var cell = new Vector2Int(queryCell.x + dx, queryCell.y + dy);
                    if (!_cells.TryGetValue(cell, out var indices))
                    {
                        continue;
                    }

                    LastVisitedCellCount++;

                    for (int i = 0; i < indices.Count; i++)
                    {
                        LastVisitedCandidateCount++;
                        var index = indices[i];
                        var data = _anchors[index];
                        var distanceSqr = (worldPosition - data.WorldPosition).sqrMagnitude;
                        var distance = Mathf.Sqrt(distanceSqr);
                        var stability = CalculateStability(data, distance);

                        if (stability <= 0)
                        {
                            continue;
                        }

                        if (stability > bestStability ||
                            (stability == bestStability && distanceSqr < bestDistanceSqr))
                        {
                            bestStability = stability;
                            bestDistanceSqr = distanceSqr;
                            bestIndex = index;
                        }
                    }
                }
            }

            if (bestIndex < 0)
            {
                return false;
            }

            result = BuildResult(_anchors[bestIndex], worldPosition, Mathf.Sqrt(bestDistanceSqr));
            return true;
        }

        private static bool TryCreateData(
            AnchorPoint anchorPoint,
            int id,
            IList<string> warnings,
            out AnchorPointData data)
        {
            data = default;

            if (anchorPoint == null)
            {
                AddWarning(warnings, "Skipped null AnchorPoint.");
                return false;
            }

            if (!anchorPoint.isActiveAndEnabled)
            {
                AddWarning(warnings, $"Skipped disabled AnchorPoint '{anchorPoint.name}'.");
                return false;
            }

            if (anchorPoint.baseStability < 1 || anchorPoint.baseStability > 10)
            {
                AddWarning(warnings, $"Skipped AnchorPoint '{anchorPoint.name}' because baseStability is outside 1-10.");
                return false;
            }

            if (anchorPoint.previewSlightRadius <= 0f)
            {
                AddWarning(warnings, $"Skipped AnchorPoint '{anchorPoint.name}' because previewSlightRadius must be greater than 0.");
                return false;
            }

            data = new AnchorPointData
            {
                Id = id,
                DebugName = anchorPoint.gameObject.name,
                WorldPosition = anchorPoint.transform.position,
                BaseStability = anchorPoint.BaseStability,
                GrabRadius = anchorPoint.GrabRadius
            };

            return true;
        }

        private static void AddWarning(IList<string> warnings, string warning)
        {
            warnings?.Add(warning);
        }

        private void AddToCell(int anchorIndex)
        {
            var cell = ProjectToCell(_anchors[anchorIndex].WorldPosition);
            if (!_cells.TryGetValue(cell, out var indices))
            {
                indices = new List<int>();
                _cells.Add(cell, indices);
            }

            indices.Add(anchorIndex);

            if (!_hasCellBounds)
            {
                _minCell = cell;
                _maxCell = cell;
                _hasCellBounds = true;
                return;
            }

            _minCell = new Vector2Int(Mathf.Min(_minCell.x, cell.x), Mathf.Min(_minCell.y, cell.y));
            _maxCell = new Vector2Int(Mathf.Max(_maxCell.x, cell.x), Mathf.Max(_maxCell.y, cell.y));
        }

        private Vector2Int ProjectToCell(Vector3 worldPosition)
        {
            var local = _planeReference != null
                ? _planeReference.InverseTransformPoint(worldPosition)
                : worldPosition;

            return new Vector2Int(
                Mathf.FloorToInt(local.x / _cellSize),
                Mathf.FloorToInt(local.y / _cellSize));
        }

        private int CalculateMaxRingToBounds(Vector2Int queryCell)
        {
            if (!_hasCellBounds)
            {
                return 0;
            }

            var dx = Mathf.Max(Mathf.Abs(queryCell.x - _minCell.x), Mathf.Abs(queryCell.x - _maxCell.x));
            var dy = Mathf.Max(Mathf.Abs(queryCell.y - _minCell.y), Mathf.Abs(queryCell.y - _maxCell.y));
            return Mathf.Max(dx, dy);
        }

        private static AnchorPointQueryResult BuildResult(
            AnchorPointData data,
            Vector3 queryPosition,
            float distance)
        {
            return new AnchorPointQueryResult
            {
                Found = true,
                Id = data.Id,
                DebugName = data.DebugName,
                WorldPosition = data.WorldPosition,
                Distance = distance,
                BaseStability = data.BaseStability,
                CurrentStability = CalculateStability(data, distance),
                GrabRadius = data.GrabRadius
            };
        }

        private static int CalculateStability(AnchorPointData data, float distance)
        {
            if (!data.IsValid || distance > data.GrabRadius)
            {
                return 0;
            }

            var normalizedDistance = Mathf.Clamp01(distance / data.GrabRadius);
            var stability = Mathf.CeilToInt(data.BaseStability * (1f - normalizedDistance));
            return Mathf.Clamp(stability, 1, data.BaseStability);
        }
    }

    [DisallowMultipleComponent]
    public sealed class LevelAnchorRegistry : MonoBehaviour, ILevelAnchorQuery, IGripQueryProvider
    {
        [Header("初始化")]
        [SerializeField] private bool rebuildOnAwake = true;
        [SerializeField] private Transform planeReference;
        [SerializeField] private LevelAnchorQuerySettings querySettings = LevelAnchorQuerySettings.CreateDefault();

        [Header("调试")]
        [SerializeField] private bool logInitialization;
        [SerializeField] private bool logQueries;
        [SerializeField] private bool drawGizmos = true;

        private readonly LevelAnchorMap _map = new LevelAnchorMap();
        private readonly List<string> _initializationWarnings = new List<string>();

        public int RegisteredCount => _map.Count;
        public int SkippedCount => _initializationWarnings.Count;
        public IReadOnlyList<string> InitializationWarnings => _initializationWarnings;
        public LevelAnchorMap Map => _map;

        private void Awake()
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
            var anchors = FindObjectsOfType<AnchorPoint>();
            Rebuild(anchors);
        }

        public void Rebuild(IList<AnchorPoint> anchors)
        {
            _initializationWarnings.Clear();
            _map.Rebuild(anchors, planeReference, querySettings, _initializationWarnings);

            if (logInitialization)
            {
                Debug.Log(
                    $"LevelAnchorRegistry initialized: registered={RegisteredCount}, skipped={SkippedCount}, cellSize={_map.CellSize:0.###}",
                    this);

                for (int i = 0; i < _initializationWarnings.Count; i++)
                {
                    Debug.LogWarning(_initializationWarnings[i], this);
                }
            }
        }

        public int GetStability(Vector3 worldPosition)
        {
            var stability = _map.GetStability(worldPosition);
            if (logQueries)
            {
                Debug.Log($"Level anchor stability query: point={worldPosition}, stability={stability}", this);
            }

            return stability;
        }

        public bool TryFindNearestAnchor(
            Vector3 worldPosition,
            out AnchorPointQueryResult result,
            float maxDistance = float.PositiveInfinity)
        {
            var found = _map.TryFindNearestAnchor(worldPosition, out result, maxDistance);
            if (logQueries)
            {
                Debug.Log(
                    $"Level anchor nearest query: point={worldPosition}, found={found}, id={result.Id}, name={result.DebugName}, " +
                    $"distance={result.Distance:0.###}, stability={result.CurrentStability}",
                    this);
            }

            return found;
        }

        public bool TryQueryGrip(Vector3 handPosition, float radius, out GripQueryResult result)
        {
            var maxDistance = Mathf.Max(0f, radius, _map.MaxGrabRadius);
            if (!TryFindNearestAnchor(handPosition, out var anchorResult, maxDistance) ||
                anchorResult.CurrentStability <= 0)
            {
                result = GripQueryResult.None();
                return false;
            }

            result = GripQueryResult.Candidate(
                ForcePointType.ValidHold,
                anchorResult.GripQuality01,
                false,
                default,
                anchorResult.Id.ToString(),
                anchorResult.DebugName);

            return true;
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
