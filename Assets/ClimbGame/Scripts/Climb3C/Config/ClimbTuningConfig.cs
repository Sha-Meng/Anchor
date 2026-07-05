using UnityEngine;

namespace ClimbGame.Climb3C.Config
{
    /// <summary>
    /// 攀爬手感与输入区总配置。字段大多为"即时生效"：运行时修改后下一帧即反映。
    /// </summary>
    [CreateAssetMenu(fileName = "ClimbTuningConfig", menuName = "ClimbGame/Climb3C/Climb Tuning Config")]
    public sealed class ClimbTuningConfig : ScriptableObject
    {
        [Header("起攀")]
        [Tooltip("角色起攀时躯干中心相对墙面中心的世界偏移")]
        public Vector3 startCenterOffset = new Vector3(0f, 0f, -0.5f);

        [Header("输入区（屏幕归一化 0..1）")]
        [Tooltip("左右输入区的分割线（0.5 表示屏幕正中一分为二）")]
        [Range(0.1f, 0.9f)] public float zoneSplit = 0.5f;

        [Tooltip("输入区左右外边距（屏幕比例），避开屏幕边缘")]
        [Range(0f, 0.2f)] public float zoneHorizontalInset = 0.02f;

        [Tooltip("输入区下方裁剪（屏幕比例），例如给底部留出 UI")]
        [Range(0f, 0.5f)] public float zoneBottomInset = 0.05f;

        [Tooltip("输入区上方裁剪（屏幕比例）")]
        [Range(0f, 0.5f)] public float zoneTopInset = 0.05f;

        [Header("触点跟随（PC / 编辑器鼠标）")]
        [Tooltip("手跟随触点目标点的平滑速度，越大越紧跟；<=0 表示瞬间跟随")]
        public float handFollowLerp = 22f;

        [Header("触点跟随（移动端 touch）")]
        [Tooltip("移动端手跟随速度；<=0 为瞬间跟手。PC 鼠标路径仍用 handFollowLerp")]
        public float mobileHandFollowLerp = 0f;

        [Tooltip("移动端 touch 采样屏幕偏移（像素），把映射点上移以避开手指遮挡")]
        public Vector2 mobileTouchScreenOffset = new Vector2(0f, 120f);

        [Tooltip("移动端贴墙 Z 平滑速度；<=0 为每帧直接贴墙")]
        public float mobileWallZSmooth = 18f;

        [Tooltip("放弃本次攀爬时，手回到默认/上一抓点的平滑速度")]
        public float handReturnLerp = 12f;

        [Header("躯干重心（跟随双手中点）")]
        [Tooltip("躯干中心相对双手中点的附加偏移（本地/世界近似）")]
        public Vector3 torsoCenterOffset = new Vector3(0f, -0.35f, 0f);

        [Tooltip("躯干重心跟随双手中点的平滑速度")]
        public float torsoFollowLerp = 8f;

        [Header("输入区可视化")]
        [Tooltip("是否在屏幕上用色块标出左右输入区")]
        public bool showInputZones = false;

        [Tooltip("左侧输入区颜色（不含透明度，透明度用下方 alpha 控制）")]
        public Color leftZoneColor = new Color(0.25f, 0.6f, 1f);

        [Tooltip("右侧输入区颜色")]
        public Color rightZoneColor = new Color(1f, 0.6f, 0.2f);

        [Tooltip("非激活侧输入区的透明度")]
        [Range(0f, 1f)] public float inactiveZoneAlpha = 0.10f;

        [Tooltip("激活侧（当前该攀爬的手）输入区的透明度")]
        [Range(0f, 1f)] public float activeZoneAlpha = 0.28f;

        [Tooltip("是否显示当前触点的小标记（PC 调试友好）")]
        public bool showTouchMarker = true;
    }
}
