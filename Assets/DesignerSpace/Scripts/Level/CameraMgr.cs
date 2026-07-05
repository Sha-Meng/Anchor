using System.Collections.Generic;
using UnityEngine;

namespace DesignerSpace
{
    /// <summary>
    /// 关卡相机管理器。
    /// FixedPoint 模式会从 transformList 读取机位，并把目标相机移动到对应位置与旋转。
    /// RuntimeControlled 模式保留给 ClimbCamera 等运行时相机脚本接管相机。
    ///
    /// 平滑模式（smoothCameraTransition）下：
    /// - fixedPointCameraRig 会 lerp 跟随跟随点，机位作为 rig 的子物体随之整体平移；
    /// - 跟随点默认由 LeftHandMagnet 与 RightHandMagnet 的中点实时计算（useHandMagnetMidpoint），
    ///   无需外部推送；关闭该开关时回退到外部通过 <see cref="SetFollowTarget"/> 设置的跟随点；
    /// - 相机在机位之间以 lerp/slerp 平滑过渡，而非瞬移。
    /// 取消勾选则回退到旧方案：相机瞬移到机位、rig 不做跟随，行为与历史一致。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    [AddComponentMenu("Anchor/Camera Mgr")]
    public sealed class CameraMgr : MonoBehaviour
    {
        public enum CameraRigMode
        {
            FixedPoint = 0,
            RuntimeControlled = 1
        }

        [Header("相机")]
        [Tooltip("需要管理的相机；留空时会尝试使用 MainCamera")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("目标相机为空时是否自动查找 Camera.main")]
        [SerializeField] private bool useMainCameraWhenEmpty = true;

        [Header("机位类型")]
        [Tooltip("FixedPoint：使用定点机位；RuntimeControlled：交给其他运行时相机脚本控制")]
        [SerializeField] private CameraRigMode rigMode = CameraRigMode.FixedPoint;

        [Header("定点机位")]
        [Tooltip("定点位相机根/Rig 引用")]
        [SerializeField] private Transform fixedPointCameraRig;

        [Tooltip("相机机位列表。每个 Transform 的世界位置和旋转就是一个相机机位")]
        [SerializeField] private List<Transform> transformList = new List<Transform>();

        [Tooltip("开局默认使用的定点机位索引")]
        [SerializeField] private int startIndex;

        [Tooltip("Start 时是否自动应用默认定点机位")]
        [SerializeField] private bool applyStartPose = true;

        [Tooltip("是否每帧保持定点机位。若同一相机上还有跟随脚本，建议开启")]
        [SerializeField] private bool keepApplyingFixedPose = true;

        [Header("平滑跟随（新方案）")]
        [Tooltip("勾选：相机在机位间 lerp 平滑过渡、rig 跟随跟随点；取消：兼容旧方案（相机瞬移，rig 不跟随）")]
        [SerializeField] private bool smoothCameraTransition = true;

        [Tooltip("是否让 fixedPointCameraRig 平滑跟随跟随点（双手磁点中点或外部设置点）。仅平滑模式生效")]
        [SerializeField] private bool followRigToTarget = true;

        [Header("跟随点来源（双手磁点中点）")]
        [Tooltip("勾选：rig 跟随点由 LeftHandMagnet 与 RightHandMagnet 的中点实时计算，无需外部推送；取消：使用外部 SetFollowTarget 设置的点")]
        [SerializeField] private bool useHandMagnetMidpoint = true;

        [Tooltip("左手磁点 Transform（可手动指定；留空则按名自动解析，磁点常在运行时创建）")]
        [SerializeField] private Transform leftHandMagnet;

        [Tooltip("右手磁点 Transform（可手动指定；留空则按名自动解析，磁点常在运行时创建）")]
        [SerializeField] private Transform rightHandMagnet;

        [Tooltip("左手磁点在场景中的对象名（用于自动解析）")]
        [SerializeField] private string leftMagnetName = "LeftHandMagnet";

        [Tooltip("右手磁点在场景中的对象名（用于自动解析）")]
        [SerializeField] private string rightMagnetName = "RightHandMagnet";

        [Tooltip("rig 跟随跟随点的插值速度，数值越大跟随越紧；<= 0 表示瞬间跟随")]
        [SerializeField] private float followLerpSpeed = 8f;

        [Tooltip("相机在机位之间过渡的插值速度，数值越大切换越快；<= 0 表示瞬间到位")]
        [SerializeField] private float poseLerpSpeed = 6f;

        private int _currentIndex = -1;
        private Vector3 _followTarget;
        private bool _hasFollowTarget;
        private bool _forceExternalFollowTarget;
        private bool _poseInitialized;

        public Camera TargetCamera => ResolveTargetCamera();
        public CameraRigMode RigMode => rigMode;
        public int CurrentIndex => _currentIndex;

        private void Start()
        {
            if (applyStartPose && rigMode == CameraRigMode.FixedPoint)
            {
                TrySwitchToFixedPose(startIndex);
            }
        }

        private void LateUpdate()
        {
            if (rigMode != CameraRigMode.FixedPoint || !keepApplyingFixedPose || _currentIndex < 0)
            {
                return;
            }

            // 先让 rig 跟随跟随点（机位作为其子物体随之平移），再对当前机位做过渡，避免相机追不上 rig。
            UpdateRigFollow();
            ApplyFixedPose(_currentIndex);
        }

        public void SetTargetCamera(Camera camera)
        {
            targetCamera = camera;
        }

        public void SetRigMode(CameraRigMode mode)
        {
            rigMode = mode;
        }

        /// <summary>是否启用平滑过渡（新方案）。取消时兼容旧的瞬移机位方案。</summary>
        public void SetSmoothCameraTransition(bool enabled)
        {
            smoothCameraTransition = enabled;
        }

        /// <summary>
        /// 设置 rig 跟随的世界跟随点（如攀爬双手锚点中点）。
        /// 仅在平滑模式且启用 followRigToTarget 时生效。
        /// </summary>
        public void SetFollowTarget(Vector3 worldPoint)
        {
            SetFollowTarget(worldPoint, false);
        }

        /// <summary>
        /// 设置 rig 跟随的世界跟随点；forceExternal 为 true 时，本帧及后续优先使用外部点，
        /// 直到再次以 forceExternal=false 设置跟随点。用于摔落等手部磁点不代表角色中心的状态。
        /// </summary>
        public void SetFollowTarget(Vector3 worldPoint, bool forceExternal)
        {
            _followTarget = worldPoint;
            _hasFollowTarget = true;
            _forceExternalFollowTarget = forceExternal;
        }

        public bool TrySwitchToFixedPose(int index)
        {
            if (!IsValidFixedPoseIndex(index))
            {
                Debug.LogWarning($"[CameraMgr] 定点机位索引无效：{index}", this);
                return false;
            }

            rigMode = CameraRigMode.FixedPoint;
            _currentIndex = index;
            // 平滑模式：只更新目标机位，过渡由 LateUpdate 驱动。
            // 旧方案：立即瞬移到机位。
            if (!smoothCameraTransition)
            {
                UpdateRigFollow();
                ApplyFixedPose(index);
            }
            return true;
        }

        public void SwitchToFixedPose(int index)
        {
            TrySwitchToFixedPose(index);
        }

        public bool TrySwitchToNextFixedPose()
        {
            if (transformList == null || transformList.Count == 0)
            {
                Debug.LogWarning("[CameraMgr] 未绑定任何定点机位。", this);
                return false;
            }

            int nextIndex = _currentIndex < 0 ? 0 : (_currentIndex + 1) % transformList.Count;
            return TrySwitchToFixedPose(nextIndex);
        }

        public bool TrySwitchToPreviousFixedPose()
        {
            if (transformList == null || transformList.Count == 0)
            {
                Debug.LogWarning("[CameraMgr] 未绑定任何定点机位。", this);
                return false;
            }

            int previousIndex = _currentIndex < 0 ? 0 : (_currentIndex - 1 + transformList.Count) % transformList.Count;
            return TrySwitchToFixedPose(previousIndex);
        }

        public bool TryApplyCurrentPose()
        {
            if (rigMode != CameraRigMode.FixedPoint)
            {
                return false;
            }

            UpdateRigFollow();
            return ApplyFixedPose(_currentIndex);
        }

        /// <summary>让 rig 平滑跟随跟随点。仅平滑模式且启用跟随、且已有跟随点时生效。</summary>
        private void UpdateRigFollow()
        {
            if (!smoothCameraTransition || !followRigToTarget) return;
            if (fixedPointCameraRig == null) return;

            // 优先使用双手磁点中点作为跟随点；解析不到磁点时回退到外部设置的跟随点。
            if (useHandMagnetMidpoint && !_forceExternalFollowTarget && TryResolveHandMidpoint(out Vector3 midpoint))
            {
                _followTarget = midpoint;
                _hasFollowTarget = true;
            }

            if (!_hasFollowTarget) return;

            if (followLerpSpeed <= 0f)
            {
                fixedPointCameraRig.position = _followTarget;
                return;
            }

            float t = 1f - Mathf.Exp(-followLerpSpeed * Time.deltaTime);
            fixedPointCameraRig.position = Vector3.Lerp(fixedPointCameraRig.position, _followTarget, t);
        }

        /// <summary>
        /// 计算 LeftHandMagnet 与 RightHandMagnet 的世界中点作为跟随点。
        /// 两个磁点都在时取中点；仅有其一时取该点；都解析不到则返回 false（交由外部跟随点兜底）。
        /// </summary>
        private bool TryResolveHandMidpoint(out Vector3 midpoint)
        {
            Transform left = ResolveLeftMagnet();
            Transform right = ResolveRightMagnet();

            if (left != null && right != null)
            {
                midpoint = (left.position + right.position) * 0.5f;
                return true;
            }

            if (left != null)
            {
                midpoint = left.position;
                return true;
            }

            if (right != null)
            {
                midpoint = right.position;
                return true;
            }

            midpoint = default;
            return false;
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

        private bool ApplyFixedPose(int index)
        {
            if (!IsValidFixedPoseIndex(index))
            {
                return false;
            }

            Camera camera = ResolveTargetCamera();
            if (camera == null)
            {
                Debug.LogWarning("[CameraMgr] 未找到目标相机，无法应用机位。", this);
                return false;
            }

            Transform pose = transformList[index];

            // 旧方案或首帧：瞬间对齐，避免起始滑动。
            if (!smoothCameraTransition || !_poseInitialized)
            {
                camera.transform.SetPositionAndRotation(pose.position, pose.rotation);
                _poseInitialized = true;
                return true;
            }

            // 平滑过渡：位置 lerp、旋转 slerp 到目标机位世界位姿。
            Transform camTransform = camera.transform;
            if (poseLerpSpeed <= 0f)
            {
                camTransform.SetPositionAndRotation(pose.position, pose.rotation);
                return true;
            }

            float t = 1f - Mathf.Exp(-poseLerpSpeed * Time.deltaTime);
            Vector3 newPos = Vector3.Lerp(camTransform.position, pose.position, t);
            Quaternion newRot = Quaternion.Slerp(camTransform.rotation, pose.rotation, t);
            camTransform.SetPositionAndRotation(newPos, newRot);
            return true;
        }

        private bool IsValidFixedPoseIndex(int index)
        {
            return transformList != null
                   && index >= 0
                   && index < transformList.Count
                   && transformList[index] != null;
        }

        private Camera ResolveTargetCamera()
        {
            if (targetCamera == null && useMainCameraWhenEmpty)
            {
                targetCamera = Camera.main;
            }

            return targetCamera;
        }

        private void OnValidate()
        {
            if (startIndex < 0)
            {
                startIndex = 0;
            }

            if (transformList != null && transformList.Count > 0 && startIndex >= transformList.Count)
            {
                startIndex = transformList.Count - 1;
            }
        }
    }
}
