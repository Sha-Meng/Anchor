using Anchor.SystemValidation;
using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using UnityEngine;

namespace ClimbGame.Climb3C.Feedback
{
    /// <summary>
    /// 靠近度触觉反馈：按"当前手到最近铆钉的距离"算出 0..1 强度与档位，
    /// 实际震动委托给 SystemValidation 的 <see cref="MobileHapticFeedbackAdapter"/>（其内部按强度做脉冲频率调制）。
    /// 档位仅用于放大镜边框等可视化反馈。
    /// </summary>
    public sealed class HapticService : MonoBehaviour
    {
        [SerializeField] private HapticConfig config;
        private MobileHapticFeedbackAdapter _adapter;

        public HapticTier CurrentTier { get; private set; } = HapticTier.None;
        public float CurrentIntensity01 { get; private set; }

        public void Configure(HapticConfig cfg, MobileHapticFeedbackAdapter adapter)
        {
            config = cfg;
            _adapter = adapter;
        }

        /// <summary>喂入当前手到最近铆钉的距离（世界单位）。</summary>
        public void UpdateProximity(float distance)
        {
            if (config == null)
            {
                CurrentTier = HapticTier.None;
                CurrentIntensity01 = 0f;
                _adapter?.SetStrength(0f);
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

            _adapter?.SetStrength(CurrentIntensity01);
        }

        /// <summary>停止靠近反馈。</summary>
        public void ClearProximity()
        {
            CurrentTier = HapticTier.None;
            CurrentIntensity01 = 0f;
            _adapter?.SetStrength(0f);
        }

        /// <summary>抓住铆钉的一次强反馈（拉满强度让 adapter 密集脉冲一下）。</summary>
        public void GrabPulse()
        {
            _adapter?.SetStrength(1f);
        }
    }
}
