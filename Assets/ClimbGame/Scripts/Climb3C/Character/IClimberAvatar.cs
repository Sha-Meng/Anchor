using ClimbGame.Climb3C.Core;
using UnityEngine;

namespace ClimbGame.Climb3C.Character
{
    /// <summary>
    /// 攀爬角色的"化身（Avatar）"抽象：只负责视觉与物理表现，不含任何玩法逻辑与运行时状态。
    /// 逻辑层（控制器）通过该接口驱动化身，从而把 avatar 与逻辑彻底解耦，
    /// 便于联机时对本地/远端玩家用同一套化身、由不同数据源驱动。
    /// </summary>
    public interface IClimberAvatar
    {
        /// <summary>躯干部件的真实世界坐标（布娃娃摔落时读取物理位置）。</summary>
        Vector3 TorsoWorldPosition { get; }

        /// <summary>进入攀爬姿态：上半身运动学、双腿物理垂摆。</summary>
        void SetupClimbPose(Vector3 center);

        /// <summary>每帧驱动运动学躯干/头随中心移动。</summary>
        void SetTorsoCenter(Vector3 center);

        /// <summary>驱动某只手臂末端到目标位（两骨 IK），返回实际手掌世界坐标。</summary>
        Vector3 DriveArm(ClimbHand hand, Vector3 handTarget, bool applySway);

        /// <summary>读取某只手当前的世界坐标。</summary>
        Vector3 GetHandPosition(ClimbHand hand);

        /// <summary>切换为全物理布娃娃并施加初速度（摔落）。</summary>
        void EnterRagdoll(Vector3 initialVelocity);
    }
}
