using System;
using ClimbGame.Climb3C.Config;
using UnityEngine;

namespace ClimbGame.Climb3C.Gameplay
{
    /// <summary>3C 相机：跟随躯干重心，摔落时略微拉远。</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ClimbCamera : MonoBehaviour
    {
        [SerializeField] private ClimbCameraConfig config;

        private Func<Vector3> _targetGetter;
        private Camera _camera;
        private bool _falling;
        private Vector3 _current;
        private bool _initialized;

        public Camera Camera => _camera != null ? _camera : (_camera = GetComponent<Camera>());

        public void Configure(ClimbCameraConfig cfg, Func<Vector3> targetGetter)
        {
            config = cfg;
            _targetGetter = targetGetter;
        }

        public void SetFalling(bool falling) => _falling = falling;

        private void LateUpdate()
        {
            if (config == null || _targetGetter == null) return;
            _camera = Camera;
            _camera.fieldOfView = config.fieldOfView;

            Vector3 target = _targetGetter();
            Vector3 offset = config.followOffset;
            if (_falling) offset.z -= config.fallZoomOut;

            Vector3 desired = target + offset;
            if (config.lockHorizontal) desired.x = offset.x;

            if (!_initialized)
            {
                _current = desired;
                _initialized = true;
            }
            else
            {
                float t = 1f - Mathf.Exp(-config.followLerp * Time.deltaTime);
                _current = Vector3.Lerp(_current, desired, t);
            }

            transform.position = _current;
            transform.rotation = Quaternion.Euler(config.pitch, 0f, 0f);
        }
    }
}
