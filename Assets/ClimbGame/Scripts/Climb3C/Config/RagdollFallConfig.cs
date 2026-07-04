using UnityEngine;

namespace ClimbGame.Climb3C.Config
{
    /// <summary>
    /// 布娃娃摔落配置。物理开关属"需重建"影响较小，数值即时生效。
    /// 原型使用自包含的简易布娃娃（基本体 + Rigidbody），后续可替换为 Ragdoll Animator 2。
    /// </summary>
    [CreateAssetMenu(fileName = "RagdollFallConfig", menuName = "ClimbGame/Climb3C/Ragdoll Fall Config")]
    public sealed class RagdollFallConfig : ScriptableObject
    {
        [Header("摔落物理")]
        [Tooltip("身体各部件质量")]
        public float partMass = 1.2f;

        [Tooltip("进入布娃娃瞬间施加的向下初速度")]
        public float initialDownSpeed = 1.5f;

        [Tooltip("进入布娃娃瞬间施加的离墙推力（远离墙面），制造脱手感")]
        public float pushFromWall = 1.2f;

        [Tooltip("布娃娃线性阻尼")]
        public float linearDrag = 0.15f;

        [Tooltip("布娃娃角阻尼")]
        public float angularDrag = 0.8f;

        [Header("恢复")]
        [Tooltip("落定后从布娃娃姿态混合回受控姿态的时长（秒）")]
        public float recoverBlendSeconds = 0.35f;

        [Header("落点规则")]
        [Tooltip("落定后是否吸附到最近铆钉（否则回到下落终点的默认姿态）")]
        public bool snapToNearestRivetOnLand = true;
    }
}
