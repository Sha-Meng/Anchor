using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using DesignerSpace;
using FIMSpace.FProceduralAnimation;
using UnityEngine;

namespace ClimbGame.Climb3C.Character
{
    /// <summary>
    /// 基于 RagdollAnimator2 + Magnet Point Kinematic 的攀爬化身：
    /// 直接使用场景中已放置的角色（如 RagDollMan），全程处于 RA2 全布娃娃状态；
    /// 左右手各挂一个运动学磁点（DragPower=2 + KinematicOnMax），把手骨钉在磁点上，身体在两点力作用下物理悬挂。
    /// 攀爬时通过移动磁点位置来驱动手（替代两骨 IK）。摔落时释放磁点让身体自由下落。
    /// </summary>
    public sealed class MagnetClimberAvatar : IClimberAvatar
    {
        private readonly GameObject _character;
        private readonly ArmRigConfig _rig;
        private readonly RagdollFallConfig _fall;
        private readonly Vector3 _initialEuler;
        private readonly float _scale;

        private Transform _root;
        private Transform _chest;
        private Transform _head;
        private Transform _lHand;
        private Transform _rHand;
        private Animator _animator;
        private RagdollAnimator2 _ra2;

        private Transform _leftMagnet;
        private Transform _rightMagnet;
        private RA2MagnetPoint _leftMP;
        private RA2MagnetPoint _rightMP;

        private Vector3 _headLookDir = Vector3.forward;
        // 场景中若存在外部磁点控制器（HandFollowController，磁点跟随指针小球），
        // 则 DriveArm 不再移动磁点，避免两套系统同帧争抢；否则由本化身按目标移动磁点。
        private bool _externalHandControl;

        public Transform Root => _root;
        public Vector3 TorsoWorldPosition => _chest != null ? _chest.position : (_root != null ? _root.position : Vector3.zero);
        public Vector3 HeadWorldPosition => _head != null ? _head.position : TorsoWorldPosition;
        public Vector3 HeadLookDirection => _headLookDir;
        public CapsuleCollider BodyCapsule => null;

        public MagnetClimberAvatar(GameObject sceneCharacter, ArmRigConfig rig, RagdollFallConfig fall, Vector3 initialEuler, float scale)
        {
            _character = sceneCharacter;
            _rig = rig;
            _fall = fall;
            _initialEuler = initialEuler;
            _scale = scale <= 0f ? 1f : scale;
        }

        public void Build(Transform parent, Vector3 center, Material bodyMat, Material handMat)
        {
            if (_character == null)
            {
                Debug.LogError("[MagnetClimberAvatar] 未提供场景角色（RagDollMan）。");
                return;
            }

            _root = _character.transform;
            _animator = _character.GetComponentInChildren<Animator>();
            _ra2 = _character.GetComponent<RagdollAnimator2>();

            _chest = FindDeep(_root, "Spine2") ?? FindDeep(_root, "Spine1") ?? FindDeep(_root, "Spine");
            _head = FindDeep(_root, "Head");
            _lHand = FindDeep(_root, "LeftHand");
            _rHand = FindDeep(_root, "RightHand");

            // 是否有外部磁点控制器（如 MainLevel2 的 HandFollowController）；有则本化身不驱动磁点。
            _externalHandControl = Object.FindObjectOfType<HandFollowController>() != null;

            // Animator 始终禁用（不需要任何动画）。初始停止 ragdoll：禁用 RagdollAnimator2 组件；
            // 此时角色为静态骨骼，root.transform 的位置/旋转/缩放驱动可见姿态。第三帧再启用组件并切 Fall。
            if (_animator != null) _animator.enabled = false;
            if (_ra2 != null) _ra2.enabled = false;
            _root.position = center;
            _root.rotation = Quaternion.Euler(_initialEuler);
        }

        /// <summary>
        /// 第二帧调用：仍在站立模式（ragdoll 停止）下确定玩家初始位置/旋转/缩放。
        /// 此时 root.transform 才驱动可见姿态，物理未开放。
        /// </summary>
        public void SetInitialTransform(Vector3 position)
        {
            _root.position = position;
            _root.rotation = Quaternion.Euler(_initialEuler);
            _root.localScale = Vector3.one * _scale;
        }

        /// <summary>
        /// 第三帧调用：开放 ragdoll——启用 RagdollAnimator2 组件并切换为全布娃娃，
        /// 挂上双手运动学磁点、钉到左右抓点。此后可见姿态由物理骨骼接管，身体在两磁点力下悬挂。
        /// </summary>
        public void CommitClimbRagdoll(Vector3 leftHold, Vector3 rightHold)
        {
            if (_ra2 != null)
            {
                _ra2.enabled = true;
                _ra2.RA2Event_SwitchToFall();
            }

            if (_leftMagnet == null) _leftMagnet = CreateMagnet("LeftHandMagnet", _lHand, out _leftMP);
            if (_rightMagnet == null) _rightMagnet = CreateMagnet("RightHandMagnet", _rHand, out _rightMP);
            _leftMagnet.position = leftHold;
            _rightMagnet.position = rightHold;
        }

        /// <summary>
        /// 把角色（布娃娃）整套骨骼移动到目标世界位置。需在磁点创建、RA2 就绪若干帧后调用，
        /// 否则 Falling 模式下 dummy 尚未初始化，TranslateTo 无效。
        /// </summary>
        public void SetRagdollPosition(Vector3 position)
        {
            if (_ra2 == null) return;
            _ra2.User_TranslateTo(position);
            _ra2.User_WarpRefresh();
        }

        private Transform CreateMagnet(string name, Transform toMove, out RA2MagnetPoint mp)
        {
            var go = new GameObject(name);
            mp = go.AddComponent<RA2MagnetPoint>();
            mp.ObjectWithRagdollAnimator = _character;
            mp.ToMove = toMove;
            mp.DragPower = 2f;         // 达到 2 且 KinematicOnMax → 运动学锁定
            mp.RotatePower = 0f;
            mp.KinematicOnMax = true;
            mp.MotionInfluence = 0f;   // 移动磁点时补偿身体物理反作用，跟随更稳
            if (toMove != null) go.transform.position = toMove.position;
            return go.transform;
        }

        public void SetupClimbPose(Vector3 center)
        {
            // 重新启用磁点吸附（从摔落恢复）
            if (_leftMP != null) _leftMP.DragPower = 2f;
            if (_rightMP != null) _rightMP.DragPower = 2f;
        }

        // 身体由物理驱动，不再用代码摆放躯干
        public void SetTorsoCenter(Vector3 center) { }

        public Vector3 DriveArm(ClimbHand hand, Vector3 handTarget, bool applySway)
        {
            // 有外部磁点控制器（HandFollowController）时不驱动磁点，避免两套系统争抢；
            // 否则由攀爬控制器移动磁点到目标位（磁点再由布娃娃把手骨拽过去）。
            if (!_externalHandControl)
            {
                Transform magnet = hand == ClimbHand.Left ? _leftMagnet : _rightMagnet;
                if (magnet != null) magnet.position = handTarget;
            }
            return GetHandPosition(hand);
        }

        public Vector3 GetHandPosition(ClimbHand hand)
        {
            Transform h = hand == ClimbHand.Left ? _lHand : _rHand;
            return h != null ? h.position : TorsoWorldPosition;
        }

        public void UpdateHeadLook(Vector3 targetWorld, bool active, Vector3 neutralForward, float maxAngleDeg, float lerpSpeed)
        {
            // 身体是布娃娃，头部不由代码旋转；仅为相机计算注视方向
            Vector3 neutral = neutralForward.sqrMagnitude > 1e-6f ? neutralForward.normalized : Vector3.forward;
            Vector3 desired = neutral;
            if (active && _head != null)
            {
                Vector3 toTarget = targetWorld - _head.position;
                if (toTarget.sqrMagnitude > 1e-4f)
                {
                    Vector3 dir = toTarget.normalized;
                    float ang = Vector3.Angle(neutral, dir);
                    if (ang > maxAngleDeg) dir = Vector3.RotateTowards(neutral, dir, maxAngleDeg * Mathf.Deg2Rad, 0f).normalized;
                    desired = dir;
                }
            }
            float t = lerpSpeed <= 0f ? 1f : 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
            _headLookDir = Vector3.Slerp(_headLookDir, desired, t).normalized;
        }

        public void EnterRagdoll(Vector3 initialVelocity)
        {
            // 释放磁点吸附 → 身体自由下落
            if (_leftMP != null) _leftMP.DragPower = 0f;
            if (_rightMP != null) _rightMP.DragPower = 0f;
            if (_ra2 != null) _ra2.RA2Event_AddFullImpact(initialVelocity);
        }

        public void AddRagdollVelocityChange(Vector3 velocityChange)
        {
            if (_ra2 != null && velocityChange.sqrMagnitude > 0.000001f)
            {
                _ra2.RA2Event_AddFullImpact(velocityChange);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
