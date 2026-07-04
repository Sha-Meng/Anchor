using ClimbGame.Climb3C.Character;
using ClimbGame.Climb3C.Config;
using UnityEngine;

namespace ClimbGame.Climb3C.Gameplay
{
    /// <summary>
    /// 3C 相机：越肩基座（相对角色头部的偏移）+ 二次 lookat（跟随头部注视方向，夹取到独立的最大角度）。
    /// 头部回到中立朝向（攀爬判定结束/未伸手）时，相机自动回归初始越肩视角。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ClimbCamera : MonoBehaviour
    {
        [SerializeField] private ClimbCameraConfig config;

        private IClimberAvatar _avatar;
        private Camera _camera;
        private bool _falling;
        private Vector3 _pos;
        private Quaternion _rot;
        private bool _initialized;

        public Camera Camera => _camera != null ? _camera : (_camera = GetComponent<Camera>());

        public void Configure(ClimbCameraConfig cfg, IClimberAvatar avatar)
        {
            config = cfg;
            _avatar = avatar;
            _initialized = false;
        }

        public void SetFalling(bool falling) => _falling = falling;

        private void LateUpdate()
        {
            if (config == null || _avatar == null) return;
            _camera = Camera;
            _camera.fieldOfView = config.fieldOfView;

            Vector3 headPos = _avatar.HeadWorldPosition;

            // 越肩基座位置
            Vector3 offset = config.overShoulderOffset;
            if (_falling) offset.z -= config.fallZoomOut;
            Vector3 desiredPos = headPos + offset;

            // 相机二次 lookat：跟随头部注视方向，但相对中立朝向独立夹取到 cameraLookMaxAngle
            Vector3 neutral = config.neutralForward.sqrMagnitude > 1e-6f
                ? config.neutralForward.normalized
                : Vector3.forward;
            // 摔落时忽略头部朝向，回归越肩中立视角
            Vector3 headDir = (!_falling && _avatar.HeadLookDirection.sqrMagnitude > 1e-6f)
                ? _avatar.HeadLookDirection.normalized
                : neutral;
            float ang = Vector3.Angle(neutral, headDir);
            Vector3 camDir = ang > config.cameraLookMaxAngle
                ? Vector3.RotateTowards(neutral, headDir, config.cameraLookMaxAngle * Mathf.Deg2Rad, 0f).normalized
                : headDir;

            Vector3 aimPoint = headPos + camDir * config.lookDistance;
            Quaternion desiredRot = Quaternion.LookRotation(aimPoint - desiredPos, Vector3.up);

            if (!_initialized)
            {
                _pos = desiredPos;
                _rot = desiredRot;
                _initialized = true;
            }
            else
            {
                float pt = 1f - Mathf.Exp(-config.positionLerp * Time.deltaTime);
                float rt = 1f - Mathf.Exp(-config.cameraLookLerp * Time.deltaTime);
                _pos = Vector3.Lerp(_pos, desiredPos, pt);
                _rot = Quaternion.Slerp(_rot, desiredRot, rt);
            }

            transform.SetPositionAndRotation(_pos, _rot);
        }
    }
}
