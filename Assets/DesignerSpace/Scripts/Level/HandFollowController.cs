using System;
using FIMSpace.FProceduralAnimation;
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
    /// - 左右手互不影响，可同时用左右手分别操作两只球。
    ///
    /// 磁力规则（本组件通过 <see cref="RA2MagnetPoint.DragPower"/> 控制手是否吸附）：
    /// - hook（按住小球）时该侧磁力恢复（DragPower = <see cref="magnetDragPowerOn"/>）。
    /// - 松手时按小球到最近 <see cref="AnchorPoint"/> 的区域判定：核心区域保持磁力；
    ///   外环区域保持磁力但通过 <see cref="StaminaPenaltyRequested"/> 请求扣减耐力；脱离锚点则取消磁力。
    /// - 两球距离约束：hook 中两球距离超出最大距离的 <see cref="hookBreakDistanceFactor"/> 倍时立刻断开该拖动侧磁力；
    ///   按下时两球距离已超出最大距离的 <see cref="hookOverDistanceFactor"/> 倍则该侧不恢复磁力。
    ///
    /// 磁点通常由 <c>MagnetClimberAvatar</c> 在运行时创建（名为 <c>LeftHandMagnet</c> / <c>RightHandMagnet</c>），
    /// 本组件支持手动指定或按名自动解析。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Anchor/Hand Follow Controller")]
    public sealed class HandFollowController : MonoBehaviour
    {
        /// <summary>松手瞬间小球相对最近锚点的区域。</summary>
        private enum GrabZone
        {
            /// <summary>核心区域：距离 &lt;= 最近锚点 CoreRadius，磁力正常。</summary>
            Core,
            /// <summary>外环区域：CoreRadius &lt; 距离 &lt;= OuterRadius，磁力正常但损失耐力。</summary>
            Outer,
            /// <summary>脱离：距离 &gt; OuterRadius（或无锚点），取消磁力。</summary>
            Detached
        }

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

        [Header("磁力控制")]
        [Tooltip("恢复磁力时写入的 DragPower（2 且 KinematicOnMax 表示吸附锁定）")]
        [SerializeField] private float magnetDragPowerOn = 2f;

        [Tooltip("取消磁力时写入的 DragPower（0 表示释放，手随布娃娃下落）")]
        [SerializeField] private float magnetDragPowerOff = 0f;

        [Tooltip("松手于外环区域时一次性损失的耐力比例（最大耐力的百分比，0.3 = 30%）")]
        [Range(0f, 1f)]
        [SerializeField] private float staminaPenaltyFraction = 0.3f;

        [Tooltip("按下 hook 时的超距因子：两球距离超出 最大距离 × 该值 则不恢复磁力")]
        [SerializeField] private float hookOverDistanceFactor = 1.0f;

        [Tooltip("hook 持续期的断磁力因子：两球距离超出 最大距离 × 该值 则立刻断开该拖动侧磁力")]
        [SerializeField] private float hookBreakDistanceFactor = 1.1f;

        /// <summary>
        /// 松手于外环区域时触发，参数为需要损失的耐力比例（最大耐力的百分比）。
        /// 供耐力系统（如 ClimbController3D）订阅扣减，避免 DesignerSpace 反向依赖 ClimbGame。
        /// </summary>
        public event Action<float> StaminaPenaltyRequested;

        private RA2MagnetPoint _leftMP;
        private RA2MagnetPoint _rightMP;
        private bool _leftHeldPrev;
        private bool _rightHeldPrev;

        // 松手区域判定用的锚点缓存，避免每次松手都全量 FindObjectsOfType。
        private AnchorPoint[] _anchors;

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

            // 左手（A 球）
            UpdateHand(
                held: controllerMgr.IsAHeld,
                active: controllerMgr.IsAActive,
                ball: controllerMgr.BallA,
                magnet: ResolveLeftMagnet(),
                mp: ResolveLeftMagnetPoint(),
                heldPrev: ref _leftHeldPrev);

            // 右手（B 球）
            UpdateHand(
                held: controllerMgr.IsBHeld,
                active: controllerMgr.IsBActive,
                ball: controllerMgr.BallB,
                magnet: ResolveRightMagnet(),
                mp: ResolveRightMagnetPoint(),
                heldPrev: ref _rightHeldPrev);
        }

        /// <summary>单侧手每帧处理：先跑磁力状态机（边沿驱动），active 时再做位置跟随。</summary>
        private void UpdateHand(bool held, bool active, Transform ball, Transform magnet, RA2MagnetPoint mp, ref bool heldPrev)
        {
            bool pressEdge = held && !heldPrev;   // hook 上升沿（按下）
            bool releaseEdge = !held && heldPrev; // 松手下降沿

            if (mp != null)
            {
                if (pressEdge)
                {
                    // 按下 hook：两球已超距则不恢复磁力，否则恢复。
                    mp.DragPower = controllerMgr.IsOverMaxDistance(hookOverDistanceFactor)
                        ? magnetDragPowerOff
                        : magnetDragPowerOn;
                }
                else if (held)
                {
                    // hook 持续：两球拉得太开（超出 10%）立刻断开该拖动侧磁力。
                    if (controllerMgr.IsOverMaxDistance(hookBreakDistanceFactor))
                    {
                        mp.DragPower = magnetDragPowerOff;
                    }
                }
                else if (releaseEdge)
                {
                    ApplyReleaseZone(mp, ball);
                }
            }

            // 位置跟随：仅在有效驱动（命中场景）时移动磁点，避免把磁点甩到无效点。
            if (active && ball != null && magnet != null)
            {
                magnet.position = FollowStep(magnet.position, ball.position);
            }

            heldPrev = held;
        }

        /// <summary>松手时按小球到最近锚点的区域决定磁力与耐力惩罚。</summary>
        private void ApplyReleaseZone(RA2MagnetPoint mp, Transform ball)
        {
            GrabZone zone = ball != null ? EvaluateZone(ball.position) : GrabZone.Detached;
            switch (zone)
            {
                case GrabZone.Core:
                    mp.DragPower = magnetDragPowerOn;
                    break;
                case GrabZone.Outer:
                    mp.DragPower = magnetDragPowerOn;
                    StaminaPenaltyRequested?.Invoke(staminaPenaltyFraction);
                    break;
                default:
                    mp.DragPower = magnetDragPowerOff;
                    break;
            }
        }

        /// <summary>遍历场景锚点，取最近锚点并按其两档半径分档。无锚点视为脱离。</summary>
        private GrabZone EvaluateZone(Vector3 ballPosition)
        {
            if (_anchors == null || _anchors.Length == 0)
            {
                _anchors = FindObjectsOfType<AnchorPoint>();
                if (_anchors == null || _anchors.Length == 0)
                {
                    return GrabZone.Detached;
                }
            }

            AnchorPoint nearest = null;
            float nearestSqr = float.PositiveInfinity;
            for (int i = 0; i < _anchors.Length; i++)
            {
                AnchorPoint anchor = _anchors[i];
                if (anchor == null)
                {
                    continue;
                }

                float sqr = (ballPosition - anchor.transform.position).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = anchor;
                }
            }

            if (nearest == null)
            {
                // 缓存里全为空引用（锚点被销毁）→ 刷新一次后仍取不到则视为脱离。
                _anchors = FindObjectsOfType<AnchorPoint>();
                return GrabZone.Detached;
            }

            float distance = Mathf.Sqrt(nearestSqr);
            if (distance <= nearest.CoreRadius)
            {
                return GrabZone.Core;
            }
            if (distance <= nearest.OuterRadius)
            {
                return GrabZone.Outer;
            }
            return GrabZone.Detached;
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

        /// <summary>返回左手磁点上的 RA2MagnetPoint（用于写 DragPower），随磁点 Transform 一起解析。</summary>
        private RA2MagnetPoint ResolveLeftMagnetPoint()
        {
            if (_leftMP == null)
            {
                Transform magnet = ResolveLeftMagnet();
                if (magnet != null) _leftMP = magnet.GetComponent<RA2MagnetPoint>();
            }

            return _leftMP;
        }

        /// <summary>返回右手磁点上的 RA2MagnetPoint（用于写 DragPower），随磁点 Transform 一起解析。</summary>
        private RA2MagnetPoint ResolveRightMagnetPoint()
        {
            if (_rightMP == null)
            {
                Transform magnet = ResolveRightMagnet();
                if (magnet != null) _rightMP = magnet.GetComponent<RA2MagnetPoint>();
            }

            return _rightMP;
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
