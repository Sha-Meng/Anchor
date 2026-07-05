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

        public ClimbStamina(StaminaConfig config, ClimberRuntimeState state)
        {
            _config = config;
            _state = state;
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

        /// <summary>一次性按最大耐力的比例扣减（如外环勉强抓握扣 30%）。fraction 取 [0,1]。</summary>
        public void DrainFraction(float fraction)
        {
            _state.Stamina = Mathf.Max(0f, _state.Stamina - _state.MaxStamina * Mathf.Clamp01(fraction));
        }

        public void Recover(float dt)
        {
            if (_config == null) return;
            _state.Stamina = Mathf.Min(_state.MaxStamina, _state.Stamina + _config.recoverPerSecond * dt);
        }

        public void ResetToRatio(float ratio)
        {
            _state.Stamina = _state.MaxStamina * Mathf.Clamp01(ratio);
        }
    }
}
