using NUnit.Framework;
using UnityEngine;

namespace Anchor.RivetRopeSystem.Tests
{
    public sealed class PlayerHealthSystemTests
    {
        private PlayerHealthSnapshot _snapshot;
        private PlayerHealthController _health;

        [SetUp]
        public void SetUp()
        {
            _health = new PlayerHealthController(() => _snapshot, snapshot => _snapshot = snapshot);
            _health.Reset("lead", new PlayerHealthSettings { MaxHealth = 100f });
        }

        [Test]
        public void Reset_InitializesCurrentHealthToMax()
        {
            Assert.AreEqual("lead", _snapshot.PlayerId);
            Assert.AreEqual(100f, _snapshot.MaxHealth, 0.001f);
            Assert.AreEqual(100f, _snapshot.CurrentHealth, 0.001f);
            Assert.IsFalse(_snapshot.IsFailed);
        }

        [Test]
        public void ApplyFallDamage_SubtractsHealthAndClampsAtZero()
        {
            _health.ApplyFallDamage(BuildDamage(35f));
            Assert.AreEqual(65f, _snapshot.CurrentHealth, 0.001f);
            Assert.IsFalse(_snapshot.IsFailed);

            _health.ApplyFallDamage(BuildDamage(90f));
            Assert.AreEqual(0f, _snapshot.CurrentHealth, 0.001f);
            Assert.IsTrue(_snapshot.IsFailed);
        }

        [Test]
        public void ApplyFallDamage_RaisesFailedEventOnce()
        {
            var failedCount = 0;
            _health.PlayerFailed += (_, _) => failedCount++;

            _health.ApplyFallDamage(BuildDamage(120f));
            _health.ApplyFallDamage(BuildDamage(120f));

            Assert.AreEqual(1, failedCount);
            Assert.IsTrue(_snapshot.IsFailed);
        }

        private static RopeFallResolution BuildDamage(float damage)
        {
            return new RopeFallResolution
            {
                FallingPlayerId = "lead",
                ProtectionState = RivetFallProtectionState.Protected,
                ProtectionRivetId = "rivet-001",
                FirstProtectionRivetId = "rivet-001",
                FirstProtectionSegmentLength = 5f,
                EstimatedFreeFallDistance = 5f,
                SuggestedDamage = damage,
                Reason = "ProtectedByRivet"
            };
        }
    }
}
