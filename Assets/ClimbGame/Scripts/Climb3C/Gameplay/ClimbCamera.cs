using ClimbGame.Climb3C.Character;
using ClimbGame.Climb3C.Config;
using UnityEngine;

namespace ClimbGame.Climb3C.Gameplay
{
    /// <summary>
    /// 3C 相机：跟随"未参与攀爬的那只手"平移（位置 = 跟随点 + 越肩偏移），朝向固定为中立朝向，
    /// 不做任何 lookat。摔落时额外后拉。跟随点由控制器每帧通过 <see cref="SetFollowTarget"/> 提供。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ClimbCamera : MonoBehaviour
    {
        [SerializeField] private ClimbCameraConfig config;

        private Camera _camera;
        private bool _falling;
        private Vector3 _pos;
        private Quaternion _rot;
        private bool _initialized;
        private Vector3 _target;
        private bool _hasTarget;

        public Camera Camera => _camera != null ? _camera : (_camera = GetComponent<Camera>());

        public void Configure(ClimbCameraConfig cfg, IClimberAvatar avatar)
        {
            config = cfg;
            _initialized = false;
            _hasTarget = false;
        }

        public void SetFalling(bool falling) => _falling = falling;

        /// <summary>设置相机跟随的世界点（未参与攀爬的手 / 摔落时的躯干）。</summary>
        public void SetFollowTarget(Vector3 worldPoint)
        {
            _target = worldPoint;
            _hasTarget = true;
        }

        private void LateUpdate()
        {
            if (config == null || !_hasTarget) return;
            _camera = Camera;
            _camera.fieldOfView = config.fieldOfView;

            // 位置：跟随点 + 越肩偏移（摔落时额外后拉）
            Vector3 offset = config.overShoulderOffset;
            if (_falling) offset.z -= config.fallZoomOut;
            Vector3 desiredPos = _target + offset;

            // 朝向固定：始终看向中立朝向，不做 lookat
            Vector3 neutral = config.neutralForward.sqrMagnitude > 1e-6f
                ? config.neutralForward.normalized
                : Vector3.forward;
            Quaternion desiredRot = Quaternion.LookRotation(neutral, Vector3.up);

            if (!_initialized)
            {
                _pos = desiredPos;
                _rot = desiredRot;
                _initialized = true;
            }
            else
            {
                float pt = 1f - Mathf.Exp(-config.positionLerp * Time.deltaTime);
                _pos = Vector3.Lerp(_pos, desiredPos, pt);
                _rot = desiredRot;
            }

            transform.SetPositionAndRotation(_pos, _rot);
        }
    }
}
