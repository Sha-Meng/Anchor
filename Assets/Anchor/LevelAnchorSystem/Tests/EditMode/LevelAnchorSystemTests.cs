using System.Collections.Generic;
using Anchor.ForceSystem;
using DesignerSpace;
using NUnit.Framework;
using UnityEngine;

namespace Anchor.LevelAnchorSystem.Tests
{
    public sealed class LevelAnchorSystemTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<string> _warnings = new List<string>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _objects.Count; i++)
            {
                Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
            _warnings.Clear();
        }

        [Test]
        public void Rebuild_RegistersValidAnchorsAndSkipsInvalidAnchors()
        {
            var valid = CreateAnchor("valid", Vector3.zero, 8, 2.5f);
            var disabled = CreateAnchor("disabled", Vector3.right, 8, 2.5f);
            disabled.enabled = false;
            var invalidStability = CreateAnchor("invalid-stability", Vector3.up, 0, 2.5f);
            var invalidRadius = CreateAnchor("invalid-radius", Vector3.forward, 8, 0f);

            var map = BuildMap(valid, disabled, invalidStability, invalidRadius);

            Assert.AreEqual(1, map.Count);
            Assert.AreEqual(3, _warnings.Count);
            Assert.AreEqual("valid", map.Anchors[0].DebugName);
            Assert.AreEqual(8, map.Anchors[0].BaseStability);
            Assert.AreEqual(2.5f, map.Anchors[0].GrabRadius);
        }

        [Test]
        public void GetStability_ReturnsZeroOutsideRadiusAndDistanceFalloffInsideRadius()
        {
            var anchor = CreateAnchor("hold", Vector3.zero, 10, 2.5f);
            var map = BuildMap(anchor);

            var center = map.GetStability(Vector3.zero);
            var nearEdge = map.GetStability(new Vector3(2.4f, 0f, 0f));
            var outside = map.GetStability(new Vector3(3f, 0f, 0f));

            Assert.AreEqual(10, center);
            Assert.Greater(center, nearEdge);
            Assert.GreaterOrEqual(nearEdge, 1);
            Assert.AreEqual(0, outside);
        }

        [Test]
        public void TryFindNearestAnchor_ReturnsNearestAnchorAndEmptyResultWhenUnavailable()
        {
            var left = CreateAnchor("left", new Vector3(-3f, 0f, 0f), 7, 2.5f);
            var right = CreateAnchor("right", new Vector3(1f, 0f, 0f), 9, 2.5f);
            var map = BuildMap(left, right);

            var found = map.TryFindNearestAnchor(Vector3.zero, out var result);
            var filtered = map.TryFindNearestAnchor(Vector3.zero, out var filteredResult, 0.5f);
            var emptyMap = BuildMap();

            Assert.IsTrue(found);
            Assert.AreEqual("right", result.DebugName);
            Assert.AreEqual(1f, result.Distance, 0.001f);
            Assert.IsFalse(filtered);
            Assert.IsFalse(filteredResult.Found);
            Assert.IsFalse(emptyMap.TryFindNearestAnchor(Vector3.zero, out _));
        }

        [Test]
        public void Queries_IgnoreZDistanceAfterPlanarCellLookup()
        {
            var raised = CreateAnchor("raised", new Vector3(0f, 0f, 3f), 10, 2.5f);
            var map = BuildMap(raised);

            var stability = map.GetStability(Vector3.zero);
            var found = map.TryFindNearestAnchor(Vector3.zero, out var nearest);

            Assert.AreEqual(10, stability);
            Assert.IsTrue(found);
            Assert.AreEqual(0f, nearest.Distance, 0.001f);
            Assert.AreEqual(10, nearest.CurrentStability);
        }

        [Test]
        public void StabilityQuery_UsesSpatialIndexWithoutVisitingAllAnchors()
        {
            var anchors = new List<AnchorPoint>();
            for (int i = 0; i < 25; i++)
            {
                anchors.Add(CreateAnchor($"anchor-{i}", new Vector3(i * 10f, 0f, 0f), 10, 1f));
            }

            var map = BuildMap(anchors.ToArray());

            Assert.AreEqual(10, map.GetStability(Vector3.zero));
            Assert.Less(map.LastVisitedCandidateCount, anchors.Count);
        }

        [Test]
        public void Registry_CanProvideGripQueryForForceSystem()
        {
            var registryObject = new GameObject("registry");
            _objects.Add(registryObject);
            var registry = registryObject.AddComponent<LevelAnchorRegistry>();
            var anchor = CreateAnchor("force-hold", Vector3.zero, 5, 2.5f);

            registry.Rebuild(new[] { anchor });

            var found = registry.TryQueryGrip(Vector3.zero, 0.25f, out var grip);

            Assert.IsTrue(found);
            Assert.AreEqual(ForcePointType.ValidHold, grip.PointType);
            Assert.AreEqual(0.5f, grip.GripQuality, 0.001f);
            Assert.AreEqual("1", grip.PointId);
            Assert.AreEqual("force-hold", grip.DebugName);
        }

        private LevelAnchorMap BuildMap(params AnchorPoint[] anchors)
        {
            var map = new LevelAnchorMap();
            map.Rebuild(
                anchors,
                null,
                new LevelAnchorQuerySettings
                {
                    CellSize = 2.5f,
                    ExtraNeighborCells = 1
                },
                _warnings);

            return map;
        }

        private AnchorPoint CreateAnchor(string name, Vector3 position, int baseStability, float radius)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            gameObject.transform.position = position;

            var anchor = gameObject.AddComponent<AnchorPoint>();
            anchor.baseStability = baseStability;
            anchor.previewSlightRadius = radius;
            return anchor;
        }
    }
}
