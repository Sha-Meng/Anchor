using System.Collections.Generic;
using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using UnityEngine;

namespace ClimbGame.Climb3C.Character
{
    /// <summary>
    /// 程序化拼装的攀爬角色：躯干 + 头 + 双臂（上臂/前臂/手）+ 双腿，全部由基本体构成。
    /// 攀爬时各部件为运动学，手臂由两骨 IK 摆放，躯干重心跟随双手中点；
    /// 耐力耗尽时切换为自包含布娃娃（Rigidbody + CharacterJoint）自由下落。
    /// 后续可将本类替换为 Ragdoll Animator 2 驱动的正式模型。
    /// </summary>
    public sealed class ClimbCharacter : IClimberAvatar
    {
        private readonly ArmRigConfig _rig;
        private readonly RagdollFallConfig _fall;

        private Transform _root;
        private BodyPart _torso;
        private BodyPart _head;
        private BodyPart _leftUpper, _leftFore, _leftHand;
        private BodyPart _rightUpper, _rightFore, _rightHand;
        private BodyPart _leftUpperLeg, _leftLowerLeg;
        private BodyPart _rightUpperLeg, _rightLowerLeg;
        private readonly List<BodyPart> _all = new List<BodyPart>();

        private Vector3 _torsoCenter;
        private bool _ragdoll;
        private CapsuleCollider _torsoCapsule;

        private const float ArmThickness = 0.07f;
        private const float LegThickness = 0.09f;

        public Vector3 TorsoCenter => _torsoCenter;
        public Transform Root => _root;
        public bool IsRagdoll => _ragdoll;

        /// <summary>躯干部件的实际世界坐标（布娃娃下落时读取真实物理位置）。</summary>
        public Vector3 TorsoWorldPosition => _torso != null ? _torso.Transform.position : _torsoCenter;

        private Vector3 _headLookDir = Vector3.forward;

        public Vector3 HeadWorldPosition => _head != null ? _head.Transform.position : _torsoCenter;
        public Vector3 HeadLookDirection => _headLookDir;
        public CapsuleCollider BodyCapsule => _torsoCapsule;

        /// <summary>头部 lookat：active 时朝 targetWorld，夹取到中立朝向 maxAngle 内；否则回中立。</summary>
        public void UpdateHeadLook(Vector3 targetWorld, bool active, Vector3 neutralForward, float maxAngleDeg, float lerpSpeed)
        {
            if (_ragdoll || _head == null) return;

            Vector3 neutral = neutralForward.sqrMagnitude > 1e-6f ? neutralForward.normalized : Vector3.forward;
            Vector3 desired = neutral;
            if (active)
            {
                Vector3 toTarget = targetWorld - _head.Transform.position;
                if (toTarget.sqrMagnitude > 1e-4f)
                {
                    Vector3 dir = toTarget.normalized;
                    float ang = Vector3.Angle(neutral, dir);
                    if (ang > maxAngleDeg)
                    {
                        dir = Vector3.RotateTowards(neutral, dir, maxAngleDeg * Mathf.Deg2Rad, 0f).normalized;
                    }
                    desired = dir;
                }
            }

            float t = lerpSpeed <= 0f ? 1f : 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
            _headLookDir = Vector3.Slerp(_headLookDir, desired, t).normalized;
            _head.Transform.rotation = Quaternion.LookRotation(_headLookDir, Vector3.up);
        }

        public ClimbCharacter(ArmRigConfig rig, RagdollFallConfig fall)
        {
            _rig = rig;
            _fall = fall;
        }

        public void Build(Transform parent, Vector3 center, Material bodyMat, Material handMat)
        {
            _torsoCenter = center;
            var rootGo = new GameObject("Climber");
            _root = rootGo.transform;
            _root.SetParent(parent, false);
            _root.position = center;

            _torso = MakeCapsule("Torso", 0.62f, 0.17f, bodyMat);
            _torsoCapsule = _torso.Transform.GetComponent<CapsuleCollider>();
            _head = MakeSphere("Head", 0.16f, bodyMat);
            _leftUpper = MakeCapsule("LeftUpperArm", _rig.upperArmLength, ArmThickness, bodyMat);
            _leftFore = MakeCapsule("LeftForearm", _rig.lowerArmLength, ArmThickness, bodyMat);
            _leftHand = MakeSphere("LeftHand", 0.1f, handMat);
            _rightUpper = MakeCapsule("RightUpperArm", _rig.upperArmLength, ArmThickness, bodyMat);
            _rightFore = MakeCapsule("RightForearm", _rig.lowerArmLength, ArmThickness, bodyMat);
            _rightHand = MakeSphere("RightHand", 0.1f, handMat);
            _leftUpperLeg = MakeCapsule("LeftUpperLeg", 0.42f, LegThickness, bodyMat);
            _leftLowerLeg = MakeCapsule("LeftLowerLeg", 0.42f, LegThickness, bodyMat);
            _rightUpperLeg = MakeCapsule("RightUpperLeg", 0.42f, LegThickness, bodyMat);
            _rightLowerLeg = MakeCapsule("RightLowerLeg", 0.42f, LegThickness, bodyMat);

            // 静止姿态摆放（用于建立布娃娃关节锚点）
            ApplyRestPose();
            SetupJoints();
            // 进入攀爬姿态：上半身运动学，双腿物理垂摆
            SetupClimbPose(center);
        }

        private BodyPart MakeCapsule(string name, float length, float radius, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = _fall != null ? _fall.partMass : 1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var col = go.AddComponent<CapsuleCollider>();
            col.direction = 1; // Y
            col.height = length + radius * 2f;
            col.radius = radius;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            if (mat != null) visual.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var part = new BodyPart(go.transform);
            _all.Add(part);
            return part;
        }

        private BodyPart MakeSphere(string name, float radius, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = _fall != null ? _fall.partMass : 1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var col = go.AddComponent<SphereCollider>();
            col.radius = radius;

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * (radius * 2f);
            if (mat != null) visual.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var part = new BodyPart(go.transform);
            _all.Add(part);
            return part;
        }

        private void ApplyRestPose()
        {
            Vector3 c = _torsoCenter;
            PlacePoint(_torso, c + new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            PlacePoint(_head, c + new Vector3(0f, 0.48f, 0f), Quaternion.identity);

            PlaceArmRest(ClimbHand.Left);
            PlaceArmRest(ClimbHand.Right);

            // 腿：自躯干下方垂下
            Vector3 lHip = c + new Vector3(-0.09f, -0.32f, 0f);
            Vector3 rHip = c + new Vector3(0.09f, -0.32f, 0f);
            PlaceBone(_leftUpperLeg, lHip, lHip + new Vector3(-0.02f, -0.42f, 0f));
            PlaceBone(_leftLowerLeg, lHip + new Vector3(-0.02f, -0.42f, 0f), lHip + new Vector3(-0.03f, -0.84f, 0f));
            PlaceBone(_rightUpperLeg, rHip, rHip + new Vector3(0.02f, -0.42f, 0f));
            PlaceBone(_rightLowerLeg, rHip + new Vector3(0.02f, -0.42f, 0f), rHip + new Vector3(0.03f, -0.84f, 0f));

            CacheRestLocal();
        }

        private void PlaceArmRest(ClimbHand hand)
        {
            Vector3 shoulder = GetShoulderWorld(hand, Vector3.zero, false);
            Vector3 handTarget = shoulder + WithSide(_rig.restHandOffset, hand);
            DriveArm(hand, handTarget, false);
        }

        private void CacheRestLocal()
        {
            Transform t = _torso.Transform;
            foreach (var p in _all)
            {
                p.RestLocalPos = t.InverseTransformPoint(p.Transform.position);
                p.RestLocalRot = Quaternion.Inverse(t.rotation) * p.Transform.rotation;
            }
        }

        private void SetupJoints()
        {
            Connect(_head, _torso);
            Connect(_leftUpper, _torso);
            Connect(_leftFore, _leftUpper);
            Connect(_leftHand, _leftFore);
            Connect(_rightUpper, _torso);
            Connect(_rightFore, _rightUpper);
            Connect(_rightHand, _rightFore);
            Connect(_leftUpperLeg, _torso);
            Connect(_leftLowerLeg, _leftUpperLeg);
            Connect(_rightUpperLeg, _torso);
            Connect(_rightLowerLeg, _rightUpperLeg);
        }

        private static void Connect(BodyPart child, BodyPart parent)
        {
            var joint = child.Transform.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent.Body;
            joint.autoConfigureConnectedAnchor = true;
            joint.enablePreprocessing = false;
            var low = joint.lowTwistLimit; low.limit = -20f; joint.lowTwistLimit = low;
            var high = joint.highTwistLimit; high.limit = 20f; joint.highTwistLimit = high;
            var swing1 = joint.swing1Limit; swing1.limit = 40f; joint.swing1Limit = swing1;
            var swing2 = joint.swing2Limit; swing2.limit = 40f; joint.swing2Limit = swing2;
        }

        /// <summary>
        /// 进入攀爬姿态：上半身（躯干/头/双臂/手）运动学，双腿切为物理动态，
        /// 通过关节从髋部自然垂摆，形成攀爬时下半身的布娃娃表现。
        /// </summary>
        public void SetupClimbPose(Vector3 center)
        {
            _ragdoll = false;
            _torsoCenter = center;

            _torso.SetKinematic(true);
            _head.SetKinematic(true);
            _leftUpper.SetKinematic(true);
            _leftFore.SetKinematic(true);
            _leftHand.SetKinematic(true);
            _rightUpper.SetKinematic(true);
            _rightFore.SetKinematic(true);
            _rightHand.SetKinematic(true);

            PlacePoint(_torso, center + new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            RestoreLocal(_head, _torso.Transform);

            // 双腿先复位到相对躯干的静止垂姿，再切物理让其自然摆动
            Transform t = _torso.Transform;
            _leftUpperLeg.SetKinematic(true);
            _leftLowerLeg.SetKinematic(true);
            _rightUpperLeg.SetKinematic(true);
            _rightLowerLeg.SetKinematic(true);
            RestoreLocal(_leftUpperLeg, t);
            RestoreLocal(_leftLowerLeg, t);
            RestoreLocal(_rightUpperLeg, t);
            RestoreLocal(_rightLowerLeg, t);
            SetLegsDynamic();
        }

        private void SetLegsDynamic()
        {
            SetLegDynamic(_leftUpperLeg);
            SetLegDynamic(_leftLowerLeg);
            SetLegDynamic(_rightUpperLeg);
            SetLegDynamic(_rightLowerLeg);
        }

        private void SetLegDynamic(BodyPart leg)
        {
            leg.SetKinematic(false);
            // 攀爬时双腿摆动的阻尼：略大以免乱甩，但保留可见的自然晃动
            leg.Body.drag = 0.4f;
            leg.Body.angularDrag = 1.5f;
            leg.Body.velocity = Vector3.zero;
            leg.Body.angularVelocity = Vector3.zero;
        }

        /// <summary>攀爬模式每帧调用：驱动运动学的躯干与头随中心移动；双腿保持物理不干预。</summary>
        public void SetTorsoCenter(Vector3 center)
        {
            _torsoCenter = center;
            if (_ragdoll) return;

            PlacePoint(_torso, center + new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            RestoreLocal(_head, _torso.Transform);
        }

        private static void RestoreLocal(BodyPart p, Transform torso)
        {
            p.Transform.position = torso.TransformPoint(p.RestLocalPos);
            p.Transform.rotation = torso.rotation * p.RestLocalRot;
        }

        /// <summary>攀爬模式：驱动某只手臂末端到 handTarget，返回实际手掌世界坐标。</summary>
        public Vector3 DriveArm(ClimbHand hand, Vector3 handTarget, bool applySway = true)
        {
            Vector3 shoulder = GetShoulderWorld(hand, handTarget, applySway);
            Vector3 hint = hand == ClimbHand.Left ? _rig.leftElbowHint : _rig.rightElbowHint;
            ArmIkResult ik = ArmIkSolver.Solve(shoulder, handTarget, _rig.upperArmLength, _rig.lowerArmLength,
                hint, _rig.maxReachRatio);

            if (hand == ClimbHand.Left)
            {
                PlaceBone(_leftUpper, ik.Shoulder, ik.Elbow);
                PlaceBone(_leftFore, ik.Elbow, ik.Hand);
                PlacePoint(_leftHand, ik.Hand, Quaternion.identity);
            }
            else
            {
                PlaceBone(_rightUpper, ik.Shoulder, ik.Elbow);
                PlaceBone(_rightFore, ik.Elbow, ik.Hand);
                PlacePoint(_rightHand, ik.Hand, Quaternion.identity);
            }
            return ik.Hand;
        }

        public Vector3 GetHandPosition(ClimbHand hand)
        {
            return hand == ClimbHand.Left ? _leftHand.Transform.position : _rightHand.Transform.position;
        }

        public Vector3 GetShoulderWorld(ClimbHand hand, Vector3 handTarget, bool applySway)
        {
            float sign = hand == ClimbHand.Left ? -1f : 1f;
            Vector3 baseShoulder = _torsoCenter + new Vector3(sign * _rig.shoulderHalfWidth, _rig.shoulderVerticalOffset, 0f);
            if (!applySway) return baseShoulder;
            Vector3 sway = Vector3.ClampMagnitude(handTarget - baseShoulder, _rig.shoulderSwayRange);
            return baseShoulder + sway * 0.5f;
        }

        // --- 布娃娃切换 ---

        public void EnterRagdoll(Vector3 initialVelocity)
        {
            _ragdoll = true;
            // 把上半身复位到相对躯干的静止姿态，使关节锚点与建立时一致，避免开启物理瞬间被约束弹飞。
            // 双腿在攀爬时本就是物理动态，位置已是合法物理姿态，无需复位（避免摔落瞬间腿部跳变）。
            Transform t = _torso.Transform;
            RestoreLocal(_head, t);
            RestoreLocal(_leftUpper, t);
            RestoreLocal(_leftFore, t);
            RestoreLocal(_leftHand, t);
            RestoreLocal(_rightUpper, t);
            RestoreLocal(_rightFore, t);
            RestoreLocal(_rightHand, t);

            foreach (var p in _all)
            {
                p.SetKinematic(false);
                p.Body.drag = _fall != null ? _fall.linearDrag : 0.1f;
                p.Body.angularDrag = _fall != null ? _fall.angularDrag : 0.8f;
                p.Body.velocity = initialVelocity;
            }
            // 给点随机扰动让姿态更自然
            _torso.Body.AddTorque(new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * 2f,
                ForceMode.VelocityChange);
        }

        public void ExitRagdoll()
        {
            _ragdoll = false;
            SetKinematic(true);
        }

        public void SetKinematic(bool kinematic)
        {
            foreach (var p in _all) p.SetKinematic(kinematic);
        }

        private static void PlacePoint(BodyPart part, Vector3 pos, Quaternion rot)
        {
            part.Transform.SetPositionAndRotation(pos, rot);
        }

        private static void PlaceBone(BodyPart part, Vector3 a, Vector3 b)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            Vector3 mid = (a + b) * 0.5f;
            Quaternion rot = len > 1e-4f ? Quaternion.FromToRotation(Vector3.up, dir / len) : Quaternion.identity;
            part.Transform.SetPositionAndRotation(mid, rot);
        }

        private Vector3 WithSide(Vector3 offset, ClimbHand hand)
        {
            float sign = hand == ClimbHand.Left ? -1f : 1f;
            return new Vector3(offset.x * sign, offset.y, offset.z);
        }
    }
}
