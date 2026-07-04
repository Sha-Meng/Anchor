using UnityEngine;

namespace ClimbGame.Climb3C.Character
{
    /// <summary>
    /// 角色身体的一个基本体部件：持有 Transform、Rigidbody、以及相对躯干的静止本地位姿。
    /// 攀爬时各部件为运动学（由代码摆放），摔落时切换为受物理驱动的布娃娃。
    /// </summary>
    public sealed class BodyPart
    {
        public readonly Transform Transform;
        public readonly Rigidbody Body;
        public Vector3 RestLocalPos;      // 相对躯干的静止位置
        public Quaternion RestLocalRot;   // 相对躯干的静止旋转

        public BodyPart(Transform transform)
        {
            Transform = transform;
            Body = transform.GetComponent<Rigidbody>();
        }

        public void SetKinematic(bool kinematic)
        {
            if (Body == null) return;
            // 先在仍为动态时清零速度，再切运动学；避免"对运动学刚体设置速度不支持"的警告
            if (kinematic && !Body.isKinematic)
            {
                Body.velocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }
            Body.isKinematic = kinematic;
            Body.useGravity = !kinematic;
        }
    }
}
