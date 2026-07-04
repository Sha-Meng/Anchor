using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using UnityEngine;

namespace ClimbGame.Climb3C.Feedback
{
    /// <summary>
    /// 靠近度触觉反馈服务。每帧由控制器喂入"当前手到最近铆钉的距离"，
    /// 据 <see cref="HapticConfig"/> 计算档位与强度，并用脉冲频率调制驱动平台后端。
    /// 支持振幅的后端直接用振幅表达强度；不支持的用脉冲密度近似"越近越强"。
    /// </summary>
    public sealed class HapticService : MonoBehaviour
    {
        [SerializeField] private HapticConfig config;

        private IHapticBackend _backend;
        private float _pulseTimer;

        public HapticTier CurrentTier { get; private set; } = HapticTier.None;
        public float CurrentIntensity01 { get; private set; }

        public void Configure(HapticConfig cfg, IHapticBackend backend)
        {
            config = cfg;
            _backend = backend;
        }

        private void Awake()
        {
            if (_backend == null) _backend = HapticBackendFactory.Create();
        }

        /// <summary>喂入当前手到最近铆钉的距离（世界单位）。</summary>
        public void UpdateProximity(float distance)
        {
            if (config == null)
            {
                CurrentTier = HapticTier.None;
                CurrentIntensity01 = 0f;
                return;
            }

            if (distance <= config.intenseRadius) CurrentTier = HapticTier.Intense;
            else if (distance <= config.slightRadius) CurrentTier = HapticTier.Slight;
            else CurrentTier = HapticTier.None;

            if (distance >= config.slightRadius)
            {
                CurrentIntensity01 = 0f;
            }
            else
            {
                float t = Mathf.InverseLerp(config.slightRadius, config.snapRadius, distance);
                t = Mathf.Pow(Mathf.Clamp01(t), Mathf.Max(0.1f, config.intensityExponent));
                CurrentIntensity01 = Mathf.Lerp(config.minIntensity, 1f, t);
            }

            DrivePulses();
        }

        /// <summary>停止靠近反馈（例如放弃或换手后短暂无目标）。</summary>
        public void ClearProximity()
        {
            CurrentTier = HapticTier.None;
            CurrentIntensity01 = 0f;
            _pulseTimer = 0f;
        }

        /// <summary>抓住铆钉的一次强脉冲。</summary>
        public void GrabPulse()
        {
            if (config == null || _backend == null) return;
            _backend.Vibrate(config.grabPulseMillis, 1f);
        }

        private void DrivePulses()
        {
            if (_backend == null || CurrentIntensity01 <= 0f) return;

            float interval = Mathf.Lerp(config.pulseIntervalFar, config.pulseIntervalNear, CurrentIntensity01);
            _pulseTimer -= Time.deltaTime;
            if (_pulseTimer <= 0f)
            {
                _pulseTimer = Mathf.Max(0.01f, interval);
                long millis = Mathf.RoundToInt(Mathf.Lerp(10f, 30f, CurrentIntensity01));
                _backend.Vibrate(millis, CurrentIntensity01);
            }
        }
    }
}
