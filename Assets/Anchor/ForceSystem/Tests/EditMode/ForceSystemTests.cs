using NUnit.Framework;
using UnityEngine;
using Anchor.ForceSystem;

namespace Anchor.ForceSystem.Tests
{
    public sealed class ForceSystemTests
    {
        private ForceEvaluationSettings _settings;
        private ForceEvaluationMemory _memory;

        [SetUp]
        public void SetUp()
        {
            _settings = ForceEvaluationSettings.CreateDefault();
            _memory = ForceEvaluationMemory.CreateDefault();
        }

        [Test]
        public void Evaluate_ReturnsStable_WhenBothHandsHaveValidGripAndStamina()
        {
            var result = Evaluate(
                ForceTestInput.Stable(),
                0.016f);

            Assert.AreEqual(ForceState.Stable, result.State);
            Assert.IsTrue(result.LeftHand.IsEffective);
            Assert.IsTrue(result.RightHand.IsEffective);
            Assert.IsFalse(result.FallTriggered);
        }

        [Test]
        public void Evaluate_ReturnsLeftHandReleased_WhenLeftHandHitsObstacle()
        {
            var input = ForceTestInput.Stable();
            input.LeftHand = ForceTestInput.Hand(ForcePointType.Obstacle, 1f);

            var result = Evaluate(input, 0.016f);

            Assert.AreEqual(ForceState.LeftHandReleased, result.State);
            Assert.IsFalse(result.LeftHand.IsEffective);
            Assert.AreEqual(ForceHandFailureReason.Obstacle, result.LeftHand.FailureReason);
            Assert.IsTrue(result.RightHand.IsEffective);
        }

        [Test]
        public void Evaluate_ReturnsRightHandReleased_WhenRightHandFakePointIsRevealed()
        {
            var input = ForceTestInput.Stable();
            input.RightHand = ForceTestInput.Hand(ForcePointType.Fake, 1f, isFakeRevealed: true);

            var result = Evaluate(input, 0.016f);

            Assert.AreEqual(ForceState.RightHandReleased, result.State);
            Assert.IsFalse(result.RightHand.IsEffective);
            Assert.AreEqual(ForceHandFailureReason.FakePoint, result.RightHand.FailureReason);
        }

        [Test]
        public void Evaluate_ReturnsInvalidGrip_WhenGripQualityIsBelowMinimum()
        {
            var input = ForceTestInput.Stable();
            input.LeftHand = ForceTestInput.Hand(ForcePointType.ValidHold, _settings.MinGripQuality - 0.01f);

            var result = Evaluate(input, 0.016f);

            Assert.AreEqual(ForceState.LeftHandReleased, result.State);
            Assert.AreEqual(ForceHandFailureReason.GripQualityTooLow, result.LeftHand.FailureReason);
        }

        [Test]
        public void Evaluate_MarksUnstableGrip_WhenGripQualityIsLowButEffective()
        {
            var input = ForceTestInput.Stable();
            input.LeftHand = ForceTestInput.Hand(ForcePointType.ValidHold, 0.4f);

            var result = Evaluate(input, 0.016f);

            Assert.AreEqual(ForceState.Stable, result.State);
            Assert.IsTrue(result.LeftHand.IsEffective);
            Assert.IsFalse(result.LeftHand.IsStableGrip);
            Assert.IsTrue(result.HasUnstableGrip);
            Assert.Greater(result.LeftHand.SuggestedStaminaCostMultiplier, 1f);
        }

        [Test]
        public void Evaluate_ReturnsReleased_WhenSingleHandStaminaIsDepleted()
        {
            var input = ForceTestInput.Stable();
            input.RightHand.Stamina = 0f;

            var result = Evaluate(input, 0.016f);

            Assert.AreEqual(ForceState.RightHandReleased, result.State);
            Assert.AreEqual(ForceHandFailureReason.StaminaDepleted, result.RightHand.FailureReason);
        }

        [Test]
        public void Evaluate_ReturnsBothHandsReleased_BeforeFallDelayExpires()
        {
            var input = ForceTestInput.Stable();
            input.LeftHand.IsTouching = false;
            input.RightHand.IsTouching = false;

            var result = Evaluate(input, _settings.BothHandsFallDelaySeconds * 0.5f);

            Assert.AreEqual(ForceState.BothHandsReleased, result.State);
            Assert.IsFalse(result.FallTriggered);
        }

        [Test]
        public void Evaluate_ReturnsFalling_WhenBothHandsRemainInvalidPastFallDelay()
        {
            var input = ForceTestInput.Stable();
            input.LeftHand.IsTouching = false;
            input.RightHand.IsTouching = false;

            Evaluate(input, _settings.BothHandsFallDelaySeconds * 0.5f);
            var result = Evaluate(input, _settings.BothHandsFallDelaySeconds * 0.6f);

            Assert.AreEqual(ForceState.Falling, result.State);
            Assert.IsTrue(result.FallTriggered);
        }

        [Test]
        public void Evaluate_DoesNotFall_WhenSingleHandRecoversWithinGrace()
        {
            var input = ForceTestInput.Stable();
            input.LeftHand.IsTouching = false;

            var released = Evaluate(input, _settings.SingleHandGraceSeconds * 0.5f);
            input.LeftHand = ForceTestInput.Hand(ForcePointType.ValidHold, 1f);
            var recovered = Evaluate(input, 0.016f);

            Assert.AreEqual(ForceState.LeftHandReleased, released.State);
            Assert.AreEqual(ForceState.Stable, recovered.State);
            Assert.IsFalse(recovered.FallTriggered);
        }

        [Test]
        public void FakeGripQueryProvider_CanBuildInputWithoutRealLevelOrCharacter()
        {
            var provider = new FakeGripQueryProvider(
                GripQueryResult.Candidate(ForcePointType.ValidHold, 1f, pointId: "left"),
                GripQueryResult.Candidate(ForcePointType.Obstacle, 1f, pointId: "right"));

            provider.TryQueryGrip(Vector3.left, 0.25f, out var leftGrip);
            provider.TryQueryGrip(Vector3.right, 0.25f, out var rightGrip);

            var input = new ForceEvaluationInput
            {
                LeftHand = HandForceInput.FromGrip(true, leftGrip, 1f, Vector3.left),
                RightHand = HandForceInput.FromGrip(true, rightGrip, 1f, Vector3.right),
                DeltaTime = 0.016f
            };

            var result = Evaluate(input, input.DeltaTime);

            Assert.AreEqual(ForceState.RightHandReleased, result.State);
            Assert.AreEqual(ForceHandFailureReason.Obstacle, result.RightHand.FailureReason);
        }

        private ForceEvaluationResult Evaluate(ForceEvaluationInput input, float deltaTime)
        {
            input.DeltaTime = deltaTime;
            return ForceEvaluator.Evaluate(input, ref _memory, _settings);
        }

        private sealed class FakeGripQueryProvider : IGripQueryProvider
        {
            private readonly GripQueryResult _left;
            private readonly GripQueryResult _right;

            public FakeGripQueryProvider(GripQueryResult left, GripQueryResult right)
            {
                _left = left;
                _right = right;
            }

            public bool TryQueryGrip(Vector3 handPosition, float radius, out GripQueryResult result)
            {
                result = handPosition.x < 0f ? _left : _right;
                return result.HasCandidate;
            }
        }

        private static class ForceTestInput
        {
            public static ForceEvaluationInput Stable()
            {
                return new ForceEvaluationInput
                {
                    LeftHand = Hand(ForcePointType.ValidHold, 1f),
                    RightHand = Hand(ForcePointType.ValidHold, 1f),
                    Body = default,
                    PreviousState = ForceState.Stable,
                    DeltaTime = 0.016f
                };
            }

            public static HandForceInput Hand(
                ForcePointType pointType,
                float gripQuality,
                bool isFakeRevealed = false,
                float stamina = 1f)
            {
                var grip = GripQueryResult.Candidate(pointType, gripQuality, isFakeRevealed);
                return HandForceInput.FromGrip(true, grip, stamina);
            }
        }
    }
}
