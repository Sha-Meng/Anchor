using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.State;
using UnityEngine;

namespace ClimbGame.Climb3C.Gameplay
{
    /// <summary>
    /// 耐力逻辑：直接读写 <see cref="ClimberRuntimeState"/> 里的耐力字段，
    /// 使耐力成为可同步的运行时数据的一部分（单一数据源）。
    /// </summary>
    public sealed class ClimbStamina
    {
        private readonly StaminaConfig _config;
        private readonly ClimberRuntimeState _state;
        private float _recoverPerSecond;

        public ClimbStamina(StaminaConfig config, ClimberRuntimeState state)
        {
            _config = config;
            _state = state;
            _recoverPerSecond = config != null ? config.recoverPerSecond : 45f;
            _state.MaxStamina = config != null ? config.maxStamina : 100f;
            _state.Stamina = _state.MaxStamina;
        }

        public float Ratio => _state.StaminaRatio;
        public bool IsEmpty => _state.Stamina <= 0f;

        public void Drain(float dt, float multiplier = 1f)
        {
            if (_config == null) return;
            _state.Stamina = Mathf.Max(0f, _state.Stamina - _config.drainPerSecond * multiplier * dt);
        }

        public void Recover(float dt)
        {
            _state.Stamina = Mathf.Min(_state.MaxStamina, _state.Stamina + _recoverPerSecond * dt);
        }

        public void Configure(float maxStamina, float recoverPerSecond, bool refill)
        {
            float currentRatio = Ratio;
            _state.MaxStamina = Mathf.Max(0.001f, maxStamina);
            _recoverPerSecond = Mathf.Max(0f, recoverPerSecond);
            _state.Stamina = refill ? _state.MaxStamina : _state.MaxStamina * currentRatio;
        }

        public void ConsumeMaxRatio(float ratio)
        {
            _state.Stamina = Mathf.Max(0f, _state.Stamina - _state.MaxStamina * Mathf.Clamp01(ratio));
        }

        public void ResetToRatio(float ratio)
        {
            _state.Stamina = _state.MaxStamina * Mathf.Clamp01(ratio);
        }
    }
}
