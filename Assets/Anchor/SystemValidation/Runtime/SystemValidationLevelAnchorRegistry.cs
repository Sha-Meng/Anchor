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
