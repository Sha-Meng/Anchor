using NUnit.Framework;
using UnityEngine;

namespace Anchor.RivetRopeSystem.Tests
{
    public sealed class RivetRopeSystemTests
    {
        private RivetRopeSettings _settings;
        private RivetRopeModel _model;

        [SetUp]
        public void SetUp()
        {
            _settings = RivetRopeSettings.CreateDefault();
            _model = new RivetRopeModel();
            _model.Reset(_settings, "lead", "second");
        }

        [Test]
        public void Reset_AssignsDefaultInventoryAndKeepsTotalRivets()
        {
            Assert.AreEqual(_settings.TotalRivets, _model.GetInventory("lead"));
            Assert.AreEqual(0, _model.GetInventory("second"));
            Assert.AreEqual(_settings.TotalRivets, _model.TotalInventoryAndPlacedCount());
        }

        [Test]
        public void TryPlaceRivet_ConsumesInventoryCreatesRivetAndSyncEvent()
        {
            var result = PlaceLeadRivet(new Vector3(0f, 4f, 0f));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(4, _model.GetInventory("lead"));
            Assert.AreEqual(1, _model.PlacedRivets.Count);
            Assert.AreEqual(_settings.TotalRivets, _model.TotalInventoryAndPlacedCount());
            Assert.AreEqual(RivetRopeEventTypes.RivetPlace, result.SyncEvent.EventType);
            Assert.AreEqual("lead", result.SyncEvent.ActorPlayerId);
            Assert.AreEqual(result.Rivet.RivetId, result.SyncEvent.RivetId);
        }

        [Test]
        public void TryPlaceRivet_FailsWhenSurfaceInvalidOrInventoryEmpty()
        {
            var invalidSurface = _model.TryPlaceRivet(new RivetPlaceRequest
            {
                PlayerId = "lead",
                Position = Vector3.up,
                IsValidSurface = false,
                IsPlayerInteractive = true
            });

            Assert.IsFalse(invalidSurface.Success);
            Assert.AreEqual(RivetRopeFailureReason.InvalidSurface, invalidSurface.FailureReason);

            for (int i = 0; i < _settings.TotalRivets; i++)
            {
                Assert.IsTrue(PlaceLeadRivet(new Vector3(0f, i, 0f)).Success);
            }

            var noInventory = PlaceLeadRivet(new Vector3(0f, 99f, 0f));
            Assert.IsFalse(noInventory.Success);
            Assert.AreEqual(RivetRopeFailureReason.NoInventory, noInventory.FailureReason);
            Assert.AreEqual(_settings.TotalRivets, _model.TotalInventoryAndPlacedCount());
        }

        [Test]
        public void TryCollectRivet_RequiresStablePlayerAndRange()
        {
            var placed = PlaceLeadRivet(new Vector3(0f, 2f, 0f)).Rivet;

            var unstable = CollectSecondRivet(placed.RivetId, placed.Position, false);
            Assert.IsFalse(unstable.Success);
            Assert.AreEqual(RivetRopeFailureReason.PlayerNotStable, unstable.FailureReason);

            var outOfRange = CollectSecondRivet(placed.RivetId, placed.Position + Vector3.right * 10f, true);
            Assert.IsFalse(outOfRange.Success);
            Assert.AreEqual(RivetRopeFailureReason.OutOfRange, outOfRange.FailureReason);

            var collected = CollectSecondRivet(placed.RivetId, placed.Position, true);
            Assert.IsTrue(collected.Success);
            Assert.AreEqual(1, _model.GetInventory("second"));
            Assert.AreEqual(0, _model.PlacedRivets.Count);
            Assert.AreEqual(_settings.TotalRivets, _model.TotalInventoryAndPlacedCount());
            Assert.AreEqual(RivetRopeEventTypes.RivetCollect, collected.SyncEvent.EventType);
        }

        [Test]
        public void TryCollectRivet_CreditsTheCollectingPlayer()
        {
            var placed = PlaceLeadRivet(new Vector3(0f, 2f, 0f)).Rivet;

            var collectedByLead = _model.TryCollectRivet(new RivetCollectRequest
            {
                PlayerId = "lead",
                RivetId = placed.RivetId,
                PlayerPosition = placed.Position,
                IsPlayerStable = true,
                IsPlayerInteractive = true
            });

            Assert.IsTrue(collectedByLead.Success);
            Assert.AreEqual(_settings.TotalRivets, _model.GetInventory("lead"));
            Assert.AreEqual(0, _model.GetInventory("second"));
            Assert.AreEqual(_settings.TotalRivets, _model.TotalInventoryAndPlacedCount());
        }

        [Test]
        public void TrySwitchLead_SwapsRolesWithoutMovingInventory()
        {
            var leadInventory = _model.GetInventory("lead");
            var secondInventory = _model.GetInventory("second");

            var switched = _model.TrySwitchLead(true, true, false, out var failureReason);

            Assert.IsTrue(switched);
            Assert.AreEqual(RivetRopeFailureReason.None, failureReason);
            Assert.AreEqual("second", _model.LeadPlayerId);
            Assert.AreEqual("lead", _model.SecondPlayerId);
            Assert.AreEqual(leadInventory, _model.GetInventory("lead"));
            Assert.AreEqual(secondInventory, _model.GetInventory("second"));
        }

        [Test]
        public void TrySwitchLead_FailsDuringFallResolution()
        {
            var switched = _model.TrySwitchLead(true, true, true, out var failureReason);

            Assert.IsFalse(switched);
            Assert.AreEqual(RivetRopeFailureReason.PlayerNotInteractive, failureReason);
            Assert.AreEqual("lead", _model.LeadPlayerId);
            Assert.AreEqual("second", _model.SecondPlayerId);
        }

        [Test]
        public void LeadSwitchEvent_AppliesOnRemoteWithoutMovingInventory()
        {
            var switched = _model.TrySwitchLead(
                true,
                true,
                false,
                out var failureReason,
                out var syncEvent);
            var remote = new RivetRopeModel();
            remote.Reset(_settings, "lead", "second");

            var applied = remote.ApplyRemoteEvent(syncEvent);

            Assert.IsTrue(switched);
            Assert.AreEqual(RivetRopeFailureReason.None, failureReason);
            Assert.AreEqual(RivetRopeEventTypes.LeadSwitch, syncEvent.EventType);
            Assert.IsTrue(applied.Success);
            Assert.AreEqual("second", remote.LeadPlayerId);
            Assert.AreEqual("lead", remote.SecondPlayerId);
            Assert.AreEqual(_settings.TotalRivets, remote.GetInventory("lead"));
            Assert.AreEqual(0, remote.GetInventory("second"));
        }

        [Test]
        public void RecoveredRivets_BecomePlaceableAfterSwitchingLead()
        {
            var placed = new PlacedRivet[_settings.TotalRivets];
            for (int i = 0; i < placed.Length; i++)
            {
                placed[i] = PlaceLeadRivet(new Vector3(0f, i, 0f)).Rivet;
            }

            Assert.IsTrue(CollectSecondRivet(placed[0].RivetId, placed[0].Position, true).Success);
            Assert.IsTrue(CollectSecondRivet(placed[1].RivetId, placed[1].Position, true).Success);

            var stillOriginalLead = PlaceLeadRivet(new Vector3(0f, 99f, 0f));
            Assert.IsFalse(stillOriginalLead.Success);
            Assert.AreEqual(RivetRopeFailureReason.NoInventory, stillOriginalLead.FailureReason);
            Assert.AreEqual(0, _model.GetInventory("lead"));
            Assert.AreEqual(2, _model.GetInventory("second"));
            Assert.AreEqual(_settings.TotalRivets, _model.TotalInventoryAndPlacedCount());

            Assert.IsTrue(_model.TrySwitchLead(true, true, false, out var failureReason));
            Assert.AreEqual(RivetRopeFailureReason.None, failureReason);
            Assert.AreEqual("second", _model.LeadPlayerId);

            var newLeadPlace = _model.TryPlaceRivet(new RivetPlaceRequest
            {
                PlayerId = _model.LeadPlayerId,
                Position = new Vector3(0f, 99f, 0f),
                IsValidSurface = true,
                IsPlayerInteractive = true
            });

            Assert.IsTrue(newLeadPlace.Success);
            Assert.AreEqual(1, _model.GetInventory("second"));
            Assert.AreEqual(_settings.TotalRivets, _model.TotalInventoryAndPlacedCount());
        }

        [Test]
        public void BuildRopePath_UsesPlacedRivetsWithoutChangingTotalRopeLength()
        {
            PlaceLeadRivet(new Vector3(0f, 5f, 0f));

            var path = _model.BuildRopePath(Vector3.zero, new Vector3(0f, 10f, 0f));

            Assert.AreEqual(3, path.Points.Length);
            Assert.AreEqual(10f, path.UsedLength, 0.001f);
            Assert.AreEqual(_settings.TotalRopeLength - 10f, path.RemainingSlack, 0.001f);
            Assert.AreEqual(RivetRopeTensionState.Slack, path.TensionState);
        }

        [Test]
        public void BuildRopePath_BecomesTautWhenPathExceedsTotalLength()
        {
            var path = _model.BuildRopePath(Vector3.zero, new Vector3(0f, 25f, 0f));

            Assert.AreEqual(RivetRopeTensionState.Taut, path.TensionState);
            Assert.AreEqual(0f, path.RemainingSlack, 0.001f);
            Assert.AreEqual(5f, path.ConstraintDistance, 0.001f);
        }

        [Test]
        public void ForceFeedback_SlackPathDoesNotActivateConstraint()
        {
            var path = _model.BuildRopePath(Vector3.zero, new Vector3(0f, 8f, 0f));

            var feedback = _model.EvaluateForceFeedback("lead", path, new Vector3(0f, 8f, 0f), Vector3.up, 0.016f);

            Assert.IsFalse(feedback.IsActive);
            Assert.AreEqual(RopeForceFeedbackReason.Slack, feedback.Reason);
            Assert.AreEqual(0f, feedback.ConstraintDistance, 0.001f);
            Assert.AreEqual(0f, feedback.TensionStrength, 0.001f);
        }

        [Test]
        public void ForceFeedback_TautDirectPathPullsUpperTowardLower()
        {
            var path = _model.BuildRopePath(Vector3.zero, new Vector3(0f, 25f, 0f));

            var feedback = _model.EvaluateForceFeedback("lead", path, new Vector3(0f, 25f, 0f), Vector3.up * 2f, 0.02f);

            Assert.IsTrue(feedback.IsActive);
            Assert.AreEqual(RopeForceFeedbackReason.Taut, feedback.Reason);
            Assert.AreEqual(5f, feedback.ConstraintDistance, 0.001f);
            AssertVectorApproximately(Vector3.down, feedback.TensionDirection);
            Assert.Greater(feedback.TensionStrength, 0f);
            Assert.Greater(feedback.SuggestedVelocityCorrection.magnitude, 0f);
        }

        [Test]
        public void ForceFeedback_SingleRivetUsesAdjacentRivetDirection()
        {
            var settings = _settings;
            settings.TotalRopeLength = 8f;
            _model.Reset(settings, "lead", "second");
            PlaceLeadRivet(new Vector3(3f, 10f, 0f));
            var upper = new Vector3(6f, 20f, 0f);
            var path = _model.BuildRopePath(Vector3.zero, upper);

            var feedback = _model.EvaluateForceFeedback("lead", path, upper, Vector3.zero, 0.02f);

            Assert.IsTrue(feedback.IsActive);
            AssertVectorApproximately((new Vector3(3f, 10f, 0f) - upper).normalized, feedback.TensionDirection);
            AssertVectorApproximately(new Vector3(3f, 10f, 0f), feedback.AdjacentConstraintPoint);
        }

        [Test]
        public void ForceFeedback_MultiRivetUsesEndpointAdjacentPoint()
        {
            var settings = _settings;
            settings.TotalRopeLength = 8f;
            _model.Reset(settings, "lead", "second");
            PlaceLeadRivet(new Vector3(-2f, 5f, 0f));
            PlaceLeadRivet(new Vector3(2f, 12f, 0f));
            PlaceLeadRivet(new Vector3(-1f, 18f, 0f));
            var upper = new Vector3(4f, 24f, 0f);
            var path = _model.BuildRopePath(Vector3.zero, upper);

            var feedback = _model.EvaluateForceFeedback("lead", path, upper, Vector3.zero, 0.02f);

            Assert.IsTrue(feedback.IsActive);
            AssertVectorApproximately((new Vector3(-1f, 18f, 0f) - upper).normalized, feedback.TensionDirection);
            AssertVectorApproximately(new Vector3(-1f, 18f, 0f), feedback.AdjacentConstraintPoint);
        }

        [Test]
        public void ForceFeedback_DisabledBySettingsDoesNotActivate()
        {
            var settings = _settings;
            settings.EnableForceFeedback = false;
            _model.Reset(settings, "lead", "second");
            var path = _model.BuildRopePath(Vector3.zero, new Vector3(0f, 25f, 0f));

            var feedback = _model.EvaluateForceFeedback("lead", path, new Vector3(0f, 25f, 0f), Vector3.up, 0.02f);

            Assert.IsFalse(feedback.IsActive);
            Assert.AreEqual(RopeForceFeedbackReason.Disabled, feedback.Reason);
        }

        [Test]
        public void VisualSettingsChanges_DoNotChangeRopeRules()
        {
            PlaceLeadRivet(new Vector3(0f, 5f, 0f));
            var before = _model.BuildRopePath(Vector3.zero, new Vector3(0f, 12f, 0f));
            var beforeFall = _model.ResolveFall("lead", Vector3.zero, new Vector3(0f, 12f, 0f));
            var beforeFeedback = _model.EvaluateForceFeedback("lead", before, new Vector3(0f, 12f, 0f), Vector3.up, 0.02f);
            var config = ScriptableObject.CreateInstance<RivetRopeConfig>();

            var visuals = RivetRopeVisualSettings.CreateDefault();
            visuals.VisualMode = RivetRopeVisualMode.VerletSegments;
            visuals.Width = 0.16f;
            visuals.SlackSagPerMeter = 0.2f;
            visuals.PhysicsDamping = 0.8f;
            visuals.SwayAmplitude = 0.2f;
            config.ConfigureRuntimeVisuals(visuals);

            var after = _model.BuildRopePath(Vector3.zero, new Vector3(0f, 12f, 0f));
            var afterFall = _model.ResolveFall("lead", Vector3.zero, new Vector3(0f, 12f, 0f));
            var afterFeedback = _model.EvaluateForceFeedback("lead", after, new Vector3(0f, 12f, 0f), Vector3.up, 0.02f);

            Assert.AreEqual(before.UsedLength, after.UsedLength, 0.001f);
            Assert.AreEqual(before.RemainingSlack, after.RemainingSlack, 0.001f);
            Assert.AreEqual(before.ConstraintDistance, after.ConstraintDistance, 0.001f);
            Assert.AreEqual(before.TensionState, after.TensionState);
            Assert.AreEqual(beforeFall.ProtectionRivetId, afterFall.ProtectionRivetId);
            Assert.AreEqual(beforeFall.SuggestedDamage, afterFall.SuggestedDamage, 0.001f);
            Assert.AreEqual(beforeFeedback.IsActive, afterFeedback.IsActive);
            Assert.AreEqual(beforeFeedback.TensionStrength, afterFeedback.TensionStrength, 0.001f);
            AssertVectorApproximately(beforeFeedback.TensionDirection, afterFeedback.TensionDirection);
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void ResolveFall_ProtectedFallDealsLessDamageThanUnprotected()
        {
            var unprotected = _model.ResolveFall("lead", Vector3.zero, new Vector3(0f, 10f, 0f));
            PlaceLeadRivet(new Vector3(0f, 5f, 0f));
            var protectedFall = _model.ResolveFall("lead", Vector3.zero, new Vector3(0f, 10f, 0f));

            Assert.IsFalse(unprotected.IsProtected);
            Assert.IsTrue(protectedFall.IsProtected);
            Assert.Less(protectedFall.SuggestedDamage, unprotected.SuggestedDamage);
            Assert.AreEqual("rivet-001", protectedFall.ProtectionRivetId);
        }

        [Test]
        public void CollectingProtectionRivet_RecomputesFallAsUnprotected()
        {
            var placed = PlaceLeadRivet(new Vector3(0f, 5f, 0f)).Rivet;
            var protectedFall = _model.ResolveFall("lead", Vector3.zero, new Vector3(0f, 10f, 0f));

            var collected = CollectSecondRivet(placed.RivetId, placed.Position, true);
            var afterCollect = _model.ResolveFall("lead", Vector3.zero, new Vector3(0f, 10f, 0f));

            Assert.IsTrue(collected.Success);
            Assert.IsTrue(protectedFall.IsProtected);
            Assert.IsFalse(afterCollect.IsProtected);
            Assert.Greater(afterCollect.SuggestedDamage, protectedFall.SuggestedDamage);
        }

        [Test]
        public void RescueClick_ReducesProtectedFallDamage()
        {
            PlaceLeadRivet(new Vector3(0f, 5f, 0f));
            var withoutRescue = _model.ResolveFall("lead", Vector3.zero, new Vector3(0f, 10f, 0f), 0f);

            _model.StartRescueWindow();
            _model.TryApplyRescueClick(true, true);
            _model.TryApplyRescueClick(true, true);
            var withRescue = _model.ResolveFall("lead", Vector3.zero, new Vector3(0f, 10f, 0f));

            Assert.Greater(_model.RescueState.PullAmount, 0f);
            Assert.Less(withRescue.EstimatedFreeFallDistance, withoutRescue.EstimatedFreeFallDistance);
            Assert.LessOrEqual(withRescue.SuggestedDamage, withoutRescue.SuggestedDamage);
        }

        [Test]
        public void RemoteEvents_ApplyOnceAndRejectDuplicate()
        {
            var placed = PlaceLeadRivet(new Vector3(0f, 5f, 0f));
            var remote = new RivetRopeModel();
            remote.Reset(_settings, "lead", "second");

            var applied = remote.ApplyRemoteEvent(placed.SyncEvent);
            var duplicate = remote.ApplyRemoteEvent(placed.SyncEvent);

            Assert.IsTrue(applied.Success);
            Assert.AreEqual(1, remote.PlacedRivets.Count);
            Assert.IsFalse(duplicate.Success);
            Assert.AreEqual(RivetRopeFailureReason.DuplicateEvent, duplicate.FailureReason);
        }

        [Test]
        public void RemoteEvents_CanCollectRivetAndRejectStaleRevision()
        {
            var placed = PlaceLeadRivet(new Vector3(0f, 5f, 0f));
            var collected = CollectSecondRivet(placed.Rivet.RivetId, placed.Rivet.Position, true);
            var remote = new RivetRopeModel();
            remote.Reset(_settings, "lead", "second");

            var applyPlace = remote.ApplyRemoteEvent(placed.SyncEvent);
            var applyCollect = remote.ApplyRemoteEvent(collected.SyncEvent);
            var stalePlace = new RivetRopeSyncEvent
            {
                EventId = "stale-place",
                EventType = RivetRopeEventTypes.RivetPlace,
                ActorPlayerId = "lead",
                RivetId = "rivet-old",
                Position = Vector3.up,
                InventoryAfter = 4,
                RopeRevision = placed.SyncEvent.RopeRevision
            };
            var stale = remote.ApplyRemoteEvent(stalePlace);

            Assert.IsTrue(applyPlace.Success);
            Assert.IsTrue(applyCollect.Success);
            Assert.AreEqual(0, remote.PlacedRivets.Count);
            Assert.AreEqual(1, remote.GetInventory("second"));
            Assert.IsFalse(stale.Success);
            Assert.AreEqual(RivetRopeFailureReason.StaleEvent, stale.FailureReason);
        }

        private RivetOperationResult PlaceLeadRivet(Vector3 position)
        {
            return _model.TryPlaceRivet(new RivetPlaceRequest
            {
                PlayerId = "lead",
                Position = position,
                IsValidSurface = true,
                IsPlayerInteractive = true
            });
        }

        private RivetOperationResult CollectSecondRivet(string rivetId, Vector3 playerPosition, bool stable)
        {
            return _model.TryCollectRivet(new RivetCollectRequest
            {
                PlayerId = "second",
                RivetId = rivetId,
                PlayerPosition = playerPosition,
                IsPlayerStable = stable,
                IsPlayerInteractive = true
            });
        }

        private static void AssertVectorApproximately(Vector3 expected, Vector3 actual, float tolerance = 0.001f)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance);
            Assert.AreEqual(expected.y, actual.y, tolerance);
            Assert.AreEqual(expected.z, actual.z, tolerance);
        }
    }
}
