using UnityEngine;

namespace DesignerSpace
{
    /// <summary>
    /// 主角手部跟随控制器（双小球）。
    ///
    /// 交互规则：
    /// - 指针射线命中点离 A/B 球哪个更近，就操作哪个小球（拾取由 <see cref="ControllerMgr"/> 负责）。
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

        [Header("双手距离约束")]
        [Tooltip("开启后：某只手跟随小球时，会被限制在“以另一只手磁点为球心、最大距离为半径”的范围内，避免两手（两球）距离过远")]
        [SerializeField] private bool constrainHandDistance = true;

        [Tooltip("两手磁点之间允许的最大距离（米）；仅夹紧本帧被驱动的那只手，另一只手保持不动")]
        [SerializeField] private float maxHandDistance = 2f;

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

            // A 球 → 左手磁点
            if (controllerMgr.IsAActive)
            {
                Transform ballA = controllerMgr.BallA;
                Transform magnet = ResolveLeftMagnet();
                Transform otherMagnet = ResolveRightMagnet();
                if (ballA != null && magnet != null)
                {
                    // 以右手磁点为锚点夹紧目标，避免左手离右手过远。
                    Vector3 target = ClampToOther(ballA.position, otherMagnet);
                    magnet.position = FollowStep(magnet.position, target);
                }
            }

            // B 球 → 右手磁点
            if (controllerMgr.IsBActive)
            {
                Transform ballB = controllerMgr.BallB;
                Transform magnet = ResolveRightMagnet();
                Transform otherMagnet = ResolveLeftMagnet();
                if (ballB != null && magnet != null)
                {
                    // 以左手磁点为锚点夹紧目标，避免右手离左手过远。
                    Vector3 target = ClampToOther(ballB.position, otherMagnet);
                    magnet.position = FollowStep(magnet.position, target);
                }
            }
        }

        /// <summary>
        /// 把被驱动手的目标位置夹紧到“以另一只手磁点为球心、maxHandDistance 为半径”的范围内，
        /// 使两手距离不超过上限。另一只手缺失或约束关闭时原样返回。
        /// </summary>
        private Vector3 ClampToOther(Vector3 desired, Transform otherMagnet)
        {
            if (!constrainHandDistance || otherMagnet == null || maxHandDistance <= 0f)
            {
                return desired;
            }

            Vector3 anchor = otherMagnet.position;
            Vector3 offset = desired - anchor;
            float distance = offset.magnitude;
            if (distance <= maxHandDistance)
            {
                return desired;
            }

            return anchor + offset * (maxHandDistance / distance);
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
