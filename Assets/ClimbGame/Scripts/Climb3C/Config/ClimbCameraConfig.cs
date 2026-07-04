using UnityEngine;

namespace ClimbGame.Climb3C.Config
{
    /// <summary>
    /// 3C 相机配置：越肩基座 + 两级 lookat（头部跟手、相机跟头，范围独立）。全部即时生效。
    /// </summary>
    [CreateAssetMenu(fileName = "ClimbCameraConfig", menuName = "ClimbGame/Climb3C/Climb Camera Config")]
    public sealed class ClimbCameraConfig : ScriptableObject
    {
        [Header("越肩基座（相对角色头部的世界偏移）")]
        [Tooltip("相机相对头部的偏移：x 右/左肩、y 上、z 后（负=在角色身后，朝墙面看）")]
        public Vector3 overShoulderOffset = new Vector3(0.6f, 0.55f, -3.2f);

        [Tooltip("相机基座跟随头部位置的平滑速度")]
        public float positionLerp = 8f;

        [Tooltip("相机瞄准点相对头部沿视线方向的前伸距离")]
        public float lookDistance = 6f;

        [Header("头部 lookat（跟随攀爬手，范围独立）")]
        [Tooltip("头部相对中立朝向偏转的最大角度（度）")]
        public float headLookMaxAngle = 35f;

        [Tooltip("头部朝向的平滑速度")]
        public float headLookLerp = 10f;

        [Header("相机二次 lookat（跟随头部朝向，范围独立）")]
        [Tooltip("相机相对中立朝向偏转的最大角度（度），独立于头部")]
        public float cameraLookMaxAngle = 18f;

        [Tooltip("相机朝向的平滑速度")]
        public float cameraLookLerp = 6f;

        [Header("摔落")]
        [Tooltip("摔落时相机额外后拉的距离")]
        public float fallZoomOut = 1.5f;

        [Header("视野")]
        [Tooltip("透视相机视场角")]
        public float fieldOfView = 55f;

        [Tooltip("角色中立朝向（面向墙面的世界方向），越肩与 lookat 都以此为基准")]
        public Vector3 neutralForward = Vector3.forward;
    }
}
