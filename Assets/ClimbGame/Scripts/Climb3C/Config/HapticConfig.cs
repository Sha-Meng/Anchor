using UnityEngine;

namespace ClimbGame.Climb3C.Config
{
    /// <summary>
    /// 铆钉靠近度触觉反馈配置。半径与强度均即时生效。
    /// </summary>
    [CreateAssetMenu(fileName = "HapticConfig", menuName = "ClimbGame/Climb3C/Haptic Config")]
    public sealed class HapticConfig : ScriptableObject
    {
        [Header("距离分档（当前手到最近铆钉的距离）")]
        [Tooltip("<= 该距离：剧烈档")]
        public float intenseRadius = 0.35f;

        [Tooltip("<= 该距离（且 > 剧烈档）：轻微档；再远则无反馈")]
        public float slightRadius = 1.1f;

        [Tooltip("<= 该距离：判定抓住铆钉并完成本次攀爬")]
        public float snapRadius = 0.14f;

        [Header("连续强度映射（0..1）")]
        [Tooltip("进入 slightRadius 后，强度随距离从该下限连续升到 1")]
        [Range(0f, 1f)] public float minIntensity = 0.05f;

        [Tooltip("靠近强度映射曲线的指数，>1 使得越近越陡增")]
        public float intensityExponent = 2f;

        [Header("脉冲频率调制（能力不足时用密度近似强度）")]
        [Tooltip("最弱时脉冲间隔（秒）")]
        public float pulseIntervalFar = 0.20f;

        [Tooltip("最强时脉冲间隔（秒）")]
        public float pulseIntervalNear = 0.03f;

        [Header("抓握瞬间强脉冲")]
        [Tooltip("抓住铆钉时触发的一次强震动时长（毫秒）")]
        public long grabPulseMillis = 60;

        [Header("调试")]
        [Tooltip("编辑器/PC 无真机时，是否输出可视化/日志回退")]
        public bool editorVisualFallback = true;
    }
}
