using UnityEngine;

namespace ClimbGame.Climb3C.Config
{
    /// <summary>
    /// 3C 相机配置。全部即时生效。
    /// </summary>
    [CreateAssetMenu(fileName = "ClimbCameraConfig", menuName = "ClimbGame/Climb3C/Climb Camera Config")]
    public sealed class ClimbCameraConfig : ScriptableObject
    {
        [Header("跟随")]
        [Tooltip("相机相对躯干重心的偏移（含离墙距离）")]
        public Vector3 followOffset = new Vector3(0f, 0.5f, -7f);

        [Tooltip("相机俯仰角（度），略微俯视以体现墙面凸起")]
        public float pitch = 6f;

        [Tooltip("跟随平滑速度")]
        public float followLerp = 4f;

        [Tooltip("相机只跟随竖直方向（X 固定居中），更符合攀爬")]
        public bool lockHorizontal = true;

        [Header("摔落")]
        [Tooltip("摔落时相机额外拉远的距离")]
        public float fallZoomOut = 1.5f;

        [Header("视野")]
        [Tooltip("透视相机的视场角")]
        public float fieldOfView = 55f;
    }
}
