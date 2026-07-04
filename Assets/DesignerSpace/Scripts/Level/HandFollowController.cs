using UnityEngine;

namespace DesignerSpace
{
    /// <summary>
    /// 主角手部跟随控制器（双小球）。
    ///
    /// 交互规则：
    /// - 屏幕左半边操作 A 球，右半边操作 B 球（分界与拾取由 <see cref="ControllerMgr"/> 负责）。
    /// - 左手磁点（LeftHandMagnet）快速 lerp 跟随 A 球；右手磁点（RightHandMagnet）快速 lerp 跟随 B 球。
    ///   由 Ragdoll Animator 2 的磁点负责把手骨刚体拽到磁点位置，从而实现手跟随。
    /// - 某侧指针松开时该侧停止驱动，手保持在磁点最后位置（由布娃娃物理接管）。
    /// - 左右手互不影响，可同时用左右手分别操作两只球。
    ///
    /// 本组件只移动磁点 Transform，不直接引用 RagdollAnimator2 类型，因此可放在无外部依赖的
    /// DesignerSpace 程序集中。磁点通常由 <c>MagnetClimberAvatar</c> 在运行时创建
    /// （名为 <c>LeftHandMagnet</c> / <c>RightHandMagnet</c>），本组件支持手动指定或按名自动解析。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Anchor/Hand Follow Controller")]
    public sealed class HandFollowController : MonoBehaviour
    {
        [Header("依赖")]
        [Tooltip("提供小球位置与指针状态的控制器；留空时自动查找场景中的 ControllerMgr")]
        [SerializeField] private ControllerMgr controllerMgr;

        [Header("手部磁点（可手动指定；留空则按名自动解析）")]
        [Tooltip("左手磁点 Transform（RagdollMagnetPoint 所在对象），跟随 A 球")]
        [SerializeField] private Transform leftHandMagnet;

        [Tooltip("右手磁点 Transform（RagdollMagnetPoint 所在对象），跟随 B 球")]
        [SerializeField] private Transform rightHandMagnet;

        [Tooltip("左手磁点在场景中的对象名（用于自动解析）")]
        [SerializeField] private string leftMagnetName = "LeftHandMagnet";

        [Tooltip("右手磁点在场景中的对象名（用于自动解析）")]
        [SerializeField] private string rightMagnetName = "RightHandMagnet";

        [Header("跟随手感")]
        [Tooltip("磁点跟随小球的平滑速度，数值越大跟随越紧；<= 0 表示瞬间跟随")]
        [SerializeField] private float followLerpSpeed = 20f;

        [Tooltip("小于该距离（米）直接吸附到小球，消除 lerp 残差抖动")]
        [SerializeField] private float snapDistance = 0.003f;

        private void LateUpdate()
        {
            if (controllerMgr == null)
            {
                controllerMgr = FindObjectOfType<ControllerMgr>();
                if (controllerMgr == null)
                {
                    return;
                }
            }

            // 左半屏：A 球 → 左手磁点
            if (controllerMgr.IsAActive)
            {
                Transform ballA = controllerMgr.BallA;
                Transform magnet = ResolveLeftMagnet();
                if (ballA != null && magnet != null)
                {
                    magnet.position = FollowStep(magnet.position, ballA.position);
                }
            }

            // 右半屏：B 球 → 右手磁点
            if (controllerMgr.IsBActive)
            {
                Transform ballB = controllerMgr.BallB;
                Transform magnet = ResolveRightMagnet();
                if (ballB != null && magnet != null)
                {
                    magnet.position = FollowStep(magnet.position, ballB.position);
                }
            }
        }

        /// <summary>返回左手磁点，必要时按名自动解析（磁点常在运行时创建）。</summary>
        private Transform ResolveLeftMagnet()
        {
            if (leftHandMagnet == null && !string.IsNullOrEmpty(leftMagnetName))
            {
                GameObject go = GameObject.Find(leftMagnetName);
                if (go != null) leftHandMagnet = go.transform;
            }

            return leftHandMagnet;
        }

        /// <summary>返回右手磁点，必要时按名自动解析（磁点常在运行时创建）。</summary>
        private Transform ResolveRightMagnet()
        {
            if (rightHandMagnet == null && !string.IsNullOrEmpty(rightMagnetName))
            {
                GameObject go = GameObject.Find(rightMagnetName);
                if (go != null) rightHandMagnet = go.transform;
            }

            return rightHandMagnet;
        }

        /// <summary>朝目标做指数平滑 lerp，逼近到位即吸附，避免残余抖动。</summary>
        private Vector3 FollowStep(Vector3 current, Vector3 target)
        {
            if (followLerpSpeed <= 0f)
            {
                return target;
            }

            if ((target - current).sqrMagnitude <= snapDistance * snapDistance)
            {
                return target;
            }

            float t = 1f - Mathf.Exp(-followLerpSpeed * Time.deltaTime);
            return Vector3.Lerp(current, target, t);
        }
    }
}
