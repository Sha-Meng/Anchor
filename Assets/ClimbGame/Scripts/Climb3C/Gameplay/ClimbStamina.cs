using ClimbGame.Climb3C.Config;
using UnityEngine;

namespace ClimbGame.Climb3C.Gameplay
{
    /// <summary>耐力条：攀爬时下降，稳定抓握时快速恢复，归零触发摔落。</summary>
    public sealed class ClimbStamina
    {
        private readonly StaminaConfig _config;
        private float _current;

        public ClimbStamina(StaminaConfig config)
        {
            _config = config;
            _current = config != null ? config.maxStamina : 100f;
        }

        public float Current => _current;
        public float Max => _config != null ? _config.maxStamina : 100f;
        public float Ratio => Max > 0f ? Mathf.Clamp01(_current / Max) : 0f;
        public bool IsEmpty => _current <= 0f;

        public void Drain(float dt, float multiplier = 1f)
        {
            if (_config == null) return;
            _current = Mathf.Max(0f, _current - _config.drainPerSecond * multiplier * dt);
        }

        public void Recover(float dt)
        {
            if (_config == null) return;
            _current = Mathf.Min(Max, _current + _config.recoverPerSecond * dt);
        }

        public void ResetToRatio(float ratio)
        {
            _current = Max * Mathf.Clamp01(ratio);
        }
    }
}
