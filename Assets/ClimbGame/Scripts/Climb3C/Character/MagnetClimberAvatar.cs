using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
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

        public Transform Root => _root;
        public Vector3 TorsoWorldPosition => _chest != null ? _chest.position : (_root != null ? _root.position : Vector3.zero);
        public Vector3 HeadWorldPosition => _head != null ? _head.position : TorsoWorldPosition;
        public Vector3 HeadLookDirection => _headLookDir;
        public CapsuleCollider BodyCapsule => null;

        public MagnetClimberAvatar(GameObject sceneCharacter, ArmRigConfig rig, RagdollFallConfig fall)
        {
            _character = sceneCharacter;
            _rig = rig;
            _fall = fall;
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

            _root.position = center;

            // 创建左右手的运动学磁点（Magnet Point Kinematic）
            _leftMagnet = CreateMagnet("LeftHandMagnet", _lHand, out _leftMP);
            _rightMagnet = CreateMagnet("RightHandMagnet", _rHand, out _rightMP);

            // 全程 RA2 全布娃娃：身体物理悬挂在两个磁点上
            if (_ra2 != null)
            {
                _ra2.RA2Event_SwitchToFall();
                // 场景里的布娃娃物理骨骼在原始位置，仅移动 root 不会移动骨骼；
                // Falling 模式下用 TranslateTo 把整套骨骼平移到起攀中心，再刷新稳定。
                _ra2.User_TranslateTo(center);
                _ra2.User_WarpRefresh();
            }
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
            // 移动磁点位置来驱动手（替代 IK）
            Transform magnet = hand == ClimbHand.Left ? _leftMagnet : _rightMagnet;
            if (magnet != null) magnet.position = handTarget;
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
