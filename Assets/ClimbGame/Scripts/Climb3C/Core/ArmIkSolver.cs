using UnityEngine;

namespace ClimbGame.Climb3C.Core
{
    /// <summary>两骨 IK 求解结果：肩、肘、手三点世界坐标。</summary>
    public struct ArmIkResult
    {
        public Vector3 Shoulder;
        public Vector3 Elbow;
        public Vector3 Hand;
    }

    /// <summary>
    /// 3D 两骨解析 IK：以手掌为末端，反算肘与手位置。
    /// 肩固定作为链根，末端超出可达范围时夹取，保证无骨长突变、无反关节。
    /// </summary>
    public static class ArmIkSolver
    {
        /// <param name="shoulder">肩关节世界坐标（链根）</param>
        /// <param name="target">期望的手掌世界目标</param>
        /// <param name="upperLen">上臂长</param>
        /// <param name="lowerLen">前臂长</param>
        /// <param name="bendHint">肘弯方向世界提示</param>
        /// <param name="maxReachRatio">最大可达半径 = (upper+lower) * ratio</param>
        public static ArmIkResult Solve(Vector3 shoulder, Vector3 target, float upperLen, float lowerLen,
            Vector3 bendHint, float maxReachRatio)
        {
            upperLen = Mathf.Max(1e-4f, upperLen);
            lowerLen = Mathf.Max(1e-4f, lowerLen);

            Vector3 toTarget = target - shoulder;
            float dist = toTarget.magnitude;

            float maxReach = (upperLen + lowerLen) * Mathf.Clamp(maxReachRatio, 0.5f, 0.999f);
            float minReach = Mathf.Abs(upperLen - lowerLen) + 1e-3f;
            float clamped = Mathf.Clamp(dist, minReach, maxReach);

            Vector3 dir = dist > 1e-5f ? toTarget / dist : Vector3.down;
            Vector3 hand = shoulder + dir * clamped;

            // 肩处夹角（上臂与肩->手方向的夹角），余弦定理
            float cosShoulder = (upperLen * upperLen + clamped * clamped - lowerLen * lowerLen)
                                / (2f * upperLen * clamped);
            cosShoulder = Mathf.Clamp(cosShoulder, -1f, 1f);
            float shoulderAngle = Mathf.Acos(cosShoulder) * Mathf.Rad2Deg;

            // 弯曲平面法线：由 肩->手 方向与弯向提示叉乘得到
            Vector3 axis = Vector3.Cross(dir, bendHint.sqrMagnitude > 1e-6f ? bendHint.normalized : Vector3.down);
            if (axis.sqrMagnitude < 1e-6f)
            {
                // 提示与方向共线，退化：任取一个正交轴
                axis = Vector3.Cross(dir, Vector3.right);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(dir, Vector3.up);
            }
            axis.Normalize();

            Vector3 upperDir = Quaternion.AngleAxis(shoulderAngle, axis) * dir;
            Vector3 elbow = shoulder + upperDir * upperLen;

            return new ArmIkResult { Shoulder = shoulder, Elbow = elbow, Hand = hand };
        }
    }
}
