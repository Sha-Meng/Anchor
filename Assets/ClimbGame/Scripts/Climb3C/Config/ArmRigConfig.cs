using UnityEngine;

namespace ClimbGame.Climb3C.Config
{
    /// <summary>
    /// 手臂骨骼与两骨 IK 参数。骨长变更属"需重建"（会改变角色体尺寸），其余即时生效。
    /// </summary>
    [CreateAssetMenu(fileName = "ArmRigConfig", menuName = "ClimbGame/Climb3C/Arm Rig Config")]
    public sealed class ArmRigConfig : ScriptableObject
    {
        [Header("骨长（世界单位）")]
        [Tooltip("上臂长度：肩到肘")]
        public float upperArmLength = 0.5f;

        [Tooltip("前臂长度：肘到手掌")]
        public float lowerArmLength = 0.5f;

        [Header("肩部")]
        [Tooltip("左右肩相对躯干中心的水平半间距")]
        public float shoulderHalfWidth = 0.28f;

        [Tooltip("肩相对躯干中心的竖直偏移")]
        public float shoulderVerticalOffset = 0.35f;

        [Tooltip("肩随手拉扯做受约束小幅移动的最大范围")]
        public float shoulderSwayRange = 0.08f;

        [Header("肘弯朝向（本地提示，通常朝身体外侧+下方）")]
        [Tooltip("左手肘弯方向提示")]
        public Vector3 leftElbowHint = new Vector3(-1f, -0.6f, -0.4f);

        [Tooltip("右手肘弯方向提示")]
        public Vector3 rightElbowHint = new Vector3(1f, -0.6f, -0.4f);

        [Header("可达范围")]
        [Tooltip("最大可达半径占总臂长的比例，避免完全伸直导致的抖动")]
        [Range(0.8f, 0.999f)] public float maxReachRatio = 0.98f;

        [Header("默认手位（相对肩的本地偏移）")]
        [Tooltip("松手/未抓握时手停靠位置相对肩的偏移")]
        public Vector3 restHandOffset = new Vector3(0f, -0.55f, 0.15f);

        public float TotalArmLength => upperArmLength + lowerArmLength;
    }
}
