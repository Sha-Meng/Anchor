using System;
using UnityEngine;

namespace Anchor.RivetRopeSystem
{
    [Serializable]
    public struct PlayerHealthSettings
    {
        [Min(1f)] public float MaxHealth;

        public static PlayerHealthSettings CreateDefault()
        {
            return new PlayerHealthSettings
            {
                MaxHealth = 100f
            };
        }

        public PlayerHealthSettings Sanitized()
        {
            return new PlayerHealthSettings
            {
                MaxHealth = Mathf.Max(1f, MaxHealth)
            };
        }
    }

    [Serializable]
    public struct PlayerHealthSnapshot
    {
        public string PlayerId;
        public float CurrentHealth;
        public float MaxHealth;
        public bool IsFailed;
        public float LastDamage;
        public string LastDamageReason;

        public float HealthRatio => MaxHealth > 0f ? Mathf.Clamp01(CurrentHealth / MaxHealth) : 0f;
    }

    public interface IPlayerHealthStateSource
    {
        PlayerHealthSnapshot HealthSnapshot { get; }
    }

    public sealed class PlayerHealthController
    {
        private readonly Func<PlayerHealthSnapshot> _read;
        private readonly Action<PlayerHealthSnapshot> _write;

        public PlayerHealthController(Func<PlayerHealthSnapshot> read, Action<PlayerHealthSnapshot> write)
        {
            _read = read;
            _write = write;
        }

        public PlayerHealthSnapshot Snapshot => _read != null ? _read() : default;

        public event Action<PlayerHealthSnapshot> HealthChanged;
        public event Action<PlayerHealthSnapshot, RopeFallResolution> PlayerFailed;

        public PlayerHealthSnapshot Reset(string playerId, PlayerHealthSettings settings)
        {
            settings = settings.Sanitized();
            var snapshot = new PlayerHealthSnapshot
            {
                PlayerId = playerId ?? string.Empty,
                CurrentHealth = settings.MaxHealth,
                MaxHealth = settings.MaxHealth,
                IsFailed = false,
                LastDamage = 0f,
                LastDamageReason = string.Empty
            };

            Write(snapshot, true);
            return snapshot;
        }

        public PlayerHealthSnapshot ApplyFallDamage(RopeFallResolution resolution)
        {
            var snapshot = Snapshot;
            if (snapshot.IsFailed)
            {
                return snapshot;
            }

            var damage = Mathf.Max(0f, resolution.SuggestedDamage);
            snapshot.CurrentHealth = Mathf.Max(0f, snapshot.CurrentHealth - damage);
            snapshot.LastDamage = damage;
            snapshot.LastDamageReason = resolution.Reason ?? string.Empty;

            var failed = snapshot.CurrentHealth <= 0f;
            snapshot.IsFailed = failed;
            Write(snapshot, true);

            if (failed)
            {
                PlayerFailed?.Invoke(snapshot, resolution);
            }

            return snapshot;
        }

        private void Write(PlayerHealthSnapshot snapshot, bool notify)
        {
            _write?.Invoke(snapshot);
            if (notify)
            {
                HealthChanged?.Invoke(snapshot);
            }
        }
    }
}
