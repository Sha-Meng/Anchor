using UnityEngine;

namespace DesignerSpace
{
    /// <summary>
    /// 关卡运行模式。Debug 用于开发期把内部信息（如撒点半径）显示到 Game 视图，Release 保留起始引导点与后续淡化散点。
    /// </summary>
    public enum LevelRunMode
    {
        Release = 0,
        Debug = 1
    }

    /// <summary>
    /// 关卡全局配置（ScriptableObject）。
    ///
    /// 作为整关的运行开关与调试可视化参数的事实来源，由 <see cref="LevelMgr"/> 持有并在运行时读取。
    /// 通过菜单 Assets/Create/Anchor/关卡全局配置 创建资源，勾选 Debug 模式即可在 Game 视图看到撒点半径。
    /// </summary>
    [CreateAssetMenu(fileName = "LevelGlobalConfig", menuName = "Anchor/关卡全局配置", order = 0)]
    public class LevelGlobalConfig : ScriptableObject
    {
        [Header("运行模式")]
        [Tooltip("Debug：在 Game 视图显示撒点位置与核心/最大半径；Release：显示起始引导点与后续淡化散点")]
        public LevelRunMode runMode = LevelRunMode.Release;

        [Header("攀爬体力")]
        [Tooltip("玩家最大体力值")]
        [Min(0.001f)]
        public float staminaMax = 100f;

        [Tooltip("稳定抓在铆钉上时每秒恢复的体力值")]
        [Min(0f)]
        public float staminaRecoverPerSecond = 45f;

        [Tooltip("小球进入 Releasing 状态时扣除的最大体力比例")]
        [Range(0f, 1f)]
        public float releasingStaminaPenaltyRatio = 0.4f;

        [Header("Debug 撒点可视化")]
        [Tooltip("核心半径（AnchorPoint.previewIntenseRadius / 剧烈档）圆环颜色")]
        public Color coreRadiusColor = new Color(1f, 0.15f, 0.15f, 0.9f);

        [Tooltip("最大半径（AnchorPoint.previewSlightRadius / 轻微档）圆环颜色")]
        public Color maxRadiusColor = new Color(1f, 0.85f, 0.1f, 0.75f);

        [Tooltip("撒点中心小点颜色")]
        public Color centerColor = Color.white;

        [Tooltip("圆环分段数，越大越圆滑")]
        [Min(8)]
        public int circleSegments = 48;

        [Tooltip("圆环线宽（世界单位）")]
        [Min(0.001f)]
        public float ringLineWidth = 0.03f;

        /// <summary>当前是否处于 Debug 模式。</summary>
        public bool IsDebug => runMode == LevelRunMode.Debug;

        /// <summary>运行时使用的最大体力值。</summary>
        public float StaminaMax => Mathf.Max(0.001f, staminaMax);

        /// <summary>运行时使用的每秒体力恢复值。</summary>
        public float StaminaRecoverPerSecond => Mathf.Max(0f, staminaRecoverPerSecond);

        /// <summary>运行时使用的 Releasing 扣体力比例。</summary>
        public float ReleasingStaminaPenaltyRatio => Mathf.Clamp01(releasingStaminaPenaltyRatio);

        private void OnValidate()
        {
            staminaMax = Mathf.Max(0.001f, staminaMax);
            staminaRecoverPerSecond = Mathf.Max(0f, staminaRecoverPerSecond);
            releasingStaminaPenaltyRatio = Mathf.Clamp01(releasingStaminaPenaltyRatio);
            circleSegments = Mathf.Max(8, circleSegments);
            ringLineWidth = Mathf.Max(0.001f, ringLineWidth);
        }
    }
}
