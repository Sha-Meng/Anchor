using ClimbGame.Climb3C.Core;
using ClimbGame.Climb3C.Gameplay;
using DesignerSpace;
using UnityEngine;

namespace ClimbGame.Climb3C.Boot
{
    /// <summary>
    /// 把攀爬控制器（<see cref="ClimbController3D"/>）的手部事件桥接到关卡相机管理器（<see cref="CameraMgr"/>）。
    ///
    /// 由于 CameraMgr 位于 Anchor.DesignerSpace 程序集（references 为空，无法引用攀爬控制器），
    /// 而 ClimbGame 属于默认 Assembly-CSharp，可同时直接引用两者，故在此做类型安全的连线：
    /// - 每帧把双手锚点中点推给 CameraMgr，让 fixedPointCameraRig 平滑跟随；
    /// - 手锚定（Grab 成功）时切到中性机位（索引 anchoredPoseIndex，默认 0）；
    /// - 手指按下开始伸手（touch 脱离）时，右手切到 rightHandPoseIndex(1)，左手切到 leftHandPoseIndex(2)。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("ClimbGame/Climb Camera Mgr Bridge")]
    public sealed class ClimbCameraMgrBridge : MonoBehaviour
    {
        [Header("引用（留空则运行时自动查找）")]
        [Tooltip("目标相机管理器；留空则 FindObjectOfType<CameraMgr>()")]
        [SerializeField] private CameraMgr cameraMgr;

        [Tooltip("本地攀爬控制器；留空则 FindObjectOfType<ClimbController3D>()（由 Climb3CLevelBinder 运行时创建）")]
        [SerializeField] private ClimbController3D controller;

        [Header("机位索引")]
        [Tooltip("手锚定（抓握成功）时使用的中性机位索引")]
        [SerializeField] private int anchoredPoseIndex = 0;

        [Tooltip("右手开始伸手（touch 脱离）时使用的机位索引")]
        [SerializeField] private int rightHandPoseIndex = 1;

        [Tooltip("左手开始伸手（touch 脱离）时使用的机位索引")]
        [SerializeField] private int leftHandPoseIndex = 2;

        [Header("跟随")]
        [Tooltip("是否每帧把双手锚点中点推给 CameraMgr 作为 rig 跟随点")]
        [SerializeField] private bool driveFollowTarget = true;

        private bool _subscribed;

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (cameraMgr == null)
            {
                cameraMgr = FindObjectOfType<CameraMgr>();
            }

            if (controller == null)
            {
                controller = FindObjectOfType<ClimbController3D>();
                if (controller != null)
                {
                    Subscribe();
                }
            }

            if (cameraMgr == null || controller == null)
            {
                return;
            }

            if (driveFollowTarget)
            {
                cameraMgr.SetFollowTarget(controller.AnchorMidpoint);
            }
        }

        private void Subscribe()
        {
            if (_subscribed || controller == null)
            {
                return;
            }

            controller.HandAnchored += OnHandAnchored;
            controller.HandReachStarted += OnHandReachStarted;
            _subscribed = true;

            // 绑定瞬间对齐到中性机位并推一次跟随点，避免首帧从错误位姿过渡。
            if (cameraMgr != null)
            {
                if (driveFollowTarget)
                {
                    cameraMgr.SetFollowTarget(controller.AnchorMidpoint);
                }
                cameraMgr.SwitchToFixedPose(anchoredPoseIndex);
            }
        }

        private void Unsubscribe()
        {
            if (!_subscribed || controller == null)
            {
                _subscribed = false;
                return;
            }

            controller.HandAnchored -= OnHandAnchored;
            controller.HandReachStarted -= OnHandReachStarted;
            _subscribed = false;
        }

        private void OnHandAnchored(ClimbHand hand)
        {
            if (cameraMgr == null)
            {
                return;
            }

            cameraMgr.SwitchToFixedPose(anchoredPoseIndex);
        }

        private void OnHandReachStarted(ClimbHand hand)
        {
            if (cameraMgr == null)
            {
                return;
            }

            int index = hand == ClimbHand.Right ? rightHandPoseIndex : leftHandPoseIndex;
            cameraMgr.SwitchToFixedPose(index);
        }
    }
}
