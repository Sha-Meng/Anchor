using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using UnityEngine;

namespace ClimbGame.Climb3C.Character
{
    /// <summary>
    /// 用正式角色 Prefab（蒙皮骨骼，如 MainAcotor_F）实现的攀爬化身：
    /// 关闭 Animator，用两骨 IK 驱动手臂骨骼、根节点跟随躯干中心、头骨做 lookat，
    /// 根上挂胶囊体+刚体（攀爬时运动学防穿模，摔落时物理下落）。实现 <see cref="IClimberAvatar"/>。
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

        private Transform _root;
        private Transform _chest;   // Spine2
        private Transform _neck;
        private Transform _head;
        private Transform _lUpper, _lFore, _lHand;
        private Transform _rUpper, _rFore, _rHand;
        private Animator _animator;
        private Rigidbody _body;
        private CapsuleCollider _capsule;

        private Quaternion _headBindLocal;
        private Vector3 _headLookDir = Vector3.forward;
        private Quaternion _facing = Quaternion.identity;
        private bool _ragdoll;

        public Transform Root => _root;
        public Vector3 TorsoWorldPosition => _root != null ? _root.position : Vector3.zero;
        public Vector3 HeadWorldPosition => _head != null ? _head.position : TorsoWorldPosition;
        public Vector3 HeadLookDirection => _headLookDir;
        public CapsuleCollider BodyCapsule => _capsule;

        public PrefabClimberAvatar(GameObject prefab, ArmRigConfig rig, RagdollFallConfig fall,
            float scale, Vector3 capsuleCenter, float capsuleHeight, float capsuleRadius)
        {
            _prefab = prefab;
            _rig = rig;
            _fall = fall;
            _scale = scale <= 0f ? 1f : scale;
            _capsuleCenter = capsuleCenter;
            _capsuleHeight = capsuleHeight;
            _capsuleRadius = capsuleRadius;
        }

        public void Build(Transform parent, Vector3 center, Material bodyMat, Material handMat)
        {
            var go = Object.Instantiate(_prefab);
            go.name = "Climber";
            _root = go.transform;
            _root.SetParent(parent, false);
            _root.localScale = Vector3.one * _scale;
            _facing = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            _root.SetPositionAndRotation(center, _facing);

            _animator = go.GetComponentInChildren<Animator>();
            if (_animator != null) _animator.enabled = false;

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

            // 胶囊体 + 刚体（防穿模 / 摔落）
            _body = go.GetComponent<Rigidbody>();
            if (_body == null) _body = go.AddComponent<Rigidbody>();
            _body.useGravity = false;
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // 若 Prefab 上已有胶囊体则沿用其配置；没有才新建并按参数设置
            _capsule = go.GetComponentInChildren<CapsuleCollider>();
            if (_capsule == null)
            {
                _capsule = go.AddComponent<CapsuleCollider>();
                _capsule.direction = 1; // Y
                _capsule.center = _capsuleCenter;
                _capsule.height = _capsuleHeight;
                _capsule.radius = _capsuleRadius;
            }
        }

        public void SetupClimbPose(Vector3 center)
        {
            _ragdoll = false;
            if (_body != null)
            {
                _body.velocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                _body.isKinematic = true;
                _body.useGravity = false;
            }
            SetTorsoCenter(center);
        }

        public void SetTorsoCenter(Vector3 center)
        {
            if (_root == null || _ragdoll) return;
            _root.rotation = _facing;
            if (_chest != null)
            {
                _root.position += center - _chest.position;
            }
            else
            {
                _root.position = center;
            }
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

            // 头骨在中立姿态基础上叠加"从中立朝向转到注视朝向"的世界旋转
            _head.localRotation = _headBindLocal;
            Quaternion worldDelta = Quaternion.FromToRotation(neutral, _headLookDir);
            _head.rotation = worldDelta * _head.rotation;
        }

        public void EnterRagdoll(Vector3 initialVelocity)
        {
            _ragdoll = true;
            if (_body == null) return;
            _body.isKinematic = false;
            _body.useGravity = true;
            _body.drag = _fall != null ? _fall.linearDrag : 0.1f;
            _body.angularDrag = _fall != null ? _fall.angularDrag : 0.8f;
            _body.velocity = initialVelocity;
            _body.AddTorque(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * 1.5f,
                ForceMode.VelocityChange);
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
