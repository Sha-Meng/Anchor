using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using FIMSpace.FProceduralAnimation;
using UnityEngine;

namespace ClimbGame.Climb3C.Character
{
    /// <summary>
    /// 用正式角色 Prefab（蒙皮骨骼 + RagdollAnimator2）实现的攀爬化身：
    /// 攀爬时启用 RA2（Standing 物理混合，身体带布娃娃物理），手臂由两骨 IK 驱动骨骼、
    /// 根节点跟随躯干中心、头骨 lookat；耐力耗尽切 RA2 全布娃娃摔落，落地混合回受控姿态。
    /// 实现 <see cref="IClimberAvatar"/>。
    /// </summary>
    public sealed class PrefabClimberAvatar : IClimberAvatar
    {
        private readonly GameObject _prefab;
        private readonly ArmRigConfig _rig;
        private readonly RagdollFallConfig _fall;
        private readonly float _scale;
        private readonly Vector3 _capsuleCenter;
        private readonly float _capsuleHeight;
        private readonly float _capsuleRadius;
        private CapsuleCollider _capsule;

        private Transform _root;
        private Transform _chest;
        private Transform _neck;
        private Transform _head;
        private Transform _lUpper, _lFore, _lHand;
        private Transform _rUpper, _rFore, _rHand;
        private Animator _animator;
        private RagdollAnimator2 _ragdoll2;

        private Quaternion _headBindLocal;
        private Vector3 _headLookDir = Vector3.forward;
        private Quaternion _facing = Quaternion.identity;
        private readonly Vector3 _initialEuler;
        private bool _ragdoll;

        public Transform Root => _root;
        public Vector3 TorsoWorldPosition => _chest != null ? _chest.position : (_root != null ? _root.position : Vector3.zero);
        public Vector3 HeadWorldPosition => _head != null ? _head.position : TorsoWorldPosition;
        public Vector3 HeadLookDirection => _headLookDir;
        public CapsuleCollider BodyCapsule => _capsule;

        public PrefabClimberAvatar(GameObject prefab, ArmRigConfig rig, RagdollFallConfig fall,
            float scale, Vector3 capsuleCenter, float capsuleHeight, float capsuleRadius)
            : this(prefab, rig, fall, scale, capsuleCenter, capsuleHeight, capsuleRadius, Vector3.zero)
        {
        }

        public PrefabClimberAvatar(GameObject prefab, ArmRigConfig rig, RagdollFallConfig fall,
            float scale, Vector3 capsuleCenter, float capsuleHeight, float capsuleRadius, Vector3 initialEuler)
        {
            _prefab = prefab;
            _rig = rig;
            _fall = fall;
            _scale = scale <= 0f ? 1f : scale;
            _capsuleCenter = capsuleCenter;
            _capsuleHeight = capsuleHeight;
            _capsuleRadius = capsuleRadius;
            _initialEuler = initialEuler;
        }

        public void Build(Transform parent, Vector3 center, Material bodyMat, Material handMat)
        {
            var go = Object.Instantiate(_prefab);
            go.name = "Climber";
            _root = go.transform;
            _root.SetParent(parent, false);
            _root.localScale = Vector3.one * _scale;
            _facing = Quaternion.Euler(_initialEuler);
            _root.SetPositionAndRotation(center, _facing);

            // 攀爬时用程序化 IK 驱动骨骼：禁用 Animator（不让动画覆盖 IK）与 RA2（其 Standing 模式会捕获
            // Animator 动画，和 IK 冲突且会空引用）。RA2 仅在摔落时启用做全布娃娃。
            _animator = go.GetComponentInChildren<Animator>();
            if (_animator != null) _animator.enabled = false;

            _ragdoll2 = go.GetComponent<RagdollAnimator2>();
            if (_ragdoll2 != null) _ragdoll2.enabled = false;

            _chest = FindDeep(_root, "Spine2") ?? FindDeep(_root, "Spine1") ?? FindDeep(_root, "Spine");
            _neck = FindDeep(_root, "Neck");
            _head = FindDeep(_root, "Head");
            _lUpper = FindDeep(_root, "LeftArm");
            _lFore = FindDeep(_root, "LeftForeArm");
            _lHand = FindDeep(_root, "LeftHand");
            _rUpper = FindDeep(_root, "RightArm");
            _rFore = FindDeep(_root, "RightForeArm");
            _rHand = FindDeep(_root, "RightHand");

            if (_head != null) _headBindLocal = _head.localRotation;

            // 防穿模胶囊体（仅根节点，避免误用 RA2 骨骼上的碰撞体）
            _capsule = go.GetComponent<CapsuleCollider>();
            if (_capsule == null)
            {
                _capsule = go.AddComponent<CapsuleCollider>();
                _capsule.direction = 1;
                _capsule.center = _capsuleCenter;
                _capsule.height = _capsuleHeight;
                _capsule.radius = _capsuleRadius;
            }
        }

        public void SetupClimbPose(Vector3 center)
        {
            _ragdoll = false;
            // 从摔落恢复：仅当 RA2 正在启用（摔落中）时切回 Standing 再关掉；初始化时 RA2 未启用，跳过避免空引用
            if (_ragdoll2 != null && _ragdoll2.enabled)
            {
                _ragdoll2.RA2Event_SwitchToStand();
                _ragdoll2.enabled = false;
            }
            if (_animator != null) _animator.enabled = false;
            SetTorsoCenter(center);
        }

        public void SetTorsoCenter(Vector3 center)
        {
            if (_root == null || _ragdoll) return;
            _root.rotation = _facing;
            if (_chest != null) _root.position += center - _chest.position;
            else _root.position = center;
        }

        public Vector3 DriveArm(ClimbHand hand, Vector3 handTarget, bool applySway)
        {
            if (_ragdoll) return GetHandPosition(hand);

            if (hand == ClimbHand.Left)
            {
                Vector3 pole = (_lUpper != null ? _lUpper.position : handTarget) + _rig.leftElbowHint;
                SolveTwoBone(_lUpper, _lFore, _lHand, handTarget, pole);
                return _lHand != null ? _lHand.position : handTarget;
            }
            else
            {
                Vector3 pole = (_rUpper != null ? _rUpper.position : handTarget) + _rig.rightElbowHint;
                SolveTwoBone(_rUpper, _rFore, _rHand, handTarget, pole);
                return _rHand != null ? _rHand.position : handTarget;
            }
        }

        public Vector3 GetHandPosition(ClimbHand hand)
        {
            Transform h = hand == ClimbHand.Left ? _lHand : _rHand;
            return h != null ? h.position : TorsoWorldPosition;
        }

        public void UpdateHeadLook(Vector3 targetWorld, bool active, Vector3 neutralForward, float maxAngleDeg, float lerpSpeed)
        {
            if (_ragdoll || _head == null) return;

            Vector3 neutral = neutralForward.sqrMagnitude > 1e-6f ? neutralForward.normalized : Vector3.forward;
            Vector3 desired = neutral;
            if (active)
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

            _head.localRotation = _headBindLocal;
            Quaternion worldDelta = Quaternion.FromToRotation(neutral, _headLookDir);
            _head.rotation = worldDelta * _head.rotation;
        }

        public void EnterRagdoll(Vector3 initialVelocity)
        {
            _ragdoll = true;
            // 摔落：启用 Animator（RA2 捕获姿态所需）与 RA2，切全布娃娃并施加冲击
            if (_animator != null) _animator.enabled = true;
            if (_ragdoll2 != null)
            {
                _ragdoll2.enabled = true;
                _ragdoll2.RA2Event_SwitchToFall();
                _ragdoll2.RA2Event_AddFullImpact(initialVelocity);
            }
        }

        public void AddRagdollVelocityChange(Vector3 velocityChange)
        {
            if (_ragdoll2 != null && velocityChange.sqrMagnitude > 0.000001f)
            {
                _ragdoll2.RA2Event_AddFullImpact(velocityChange);
            }
        }

        // --- 两骨解析 IK（设置骨骼旋转，末端 end 到达 target；pole 提供弯曲朝向）---
        private static void SolveTwoBone(Transform aT, Transform bT, Transform cT, Vector3 target, Vector3 pole)
        {
            if (aT == null || bT == null || cT == null) return;

            Vector3 a = aT.position, b = bT.position, c = cT.position;
            Vector3 ab = b - a, cb = b - c, ac = c - a, at = target - a;

            float lab = ab.magnitude, lcb = cb.magnitude;
            float lat = Mathf.Clamp(at.magnitude, 0.001f, lab + lcb - 0.001f);
            if (lab < 1e-5f || lcb < 1e-5f) return;

            float ac_ab_0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ac.normalized, ab.normalized), -1f, 1f));
            float ba_bc_0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((a - b).normalized, (c - b).normalized), -1f, 1f));
            float ac_at_0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ac.normalized, at.normalized), -1f, 1f));

            float ac_ab_1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
            float ba_bc_1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

            Vector3 axis0 = Vector3.Cross(ac, ab);
            if (axis0.sqrMagnitude < 1e-8f) axis0 = Vector3.Cross(at, pole - a);
            axis0 = axis0.normalized;

            Vector3 axis1 = Vector3.Cross(ac, at);
            if (axis1.sqrMagnitude < 1e-8f) axis1 = axis0;
            axis1 = axis1.normalized;

            Quaternion r0 = Quaternion.AngleAxis((ac_ab_1 - ac_ab_0) * Mathf.Rad2Deg, Quaternion.Inverse(aT.rotation) * axis0);
            Quaternion r1 = Quaternion.AngleAxis((ba_bc_1 - ba_bc_0) * Mathf.Rad2Deg, Quaternion.Inverse(bT.rotation) * axis0);
            Quaternion r2 = Quaternion.AngleAxis(ac_at_0 * Mathf.Rad2Deg, Quaternion.Inverse(aT.rotation) * axis1);

            aT.localRotation = aT.localRotation * r0 * r2;
            bT.localRotation = bT.localRotation * r1;
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
