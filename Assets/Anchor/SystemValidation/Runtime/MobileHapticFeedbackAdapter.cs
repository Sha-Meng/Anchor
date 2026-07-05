using UnityEngine;

namespace Anchor.SystemValidation
{
    [DisallowMultipleComponent]
    public sealed class MobileHapticFeedbackAdapter : MonoBehaviour
    {
        [SerializeField] private bool enableHaptics = true;
        [SerializeField] private bool enableInEditor;
        [SerializeField] private float minStrength = 0.05f;
        [SerializeField] private float weakestPulseInterval = 0.45f;
        [SerializeField] private float strongestPulseInterval = 0.08f;

        private float _strength;
        private float _nextPulseTime;

        public void SetStrength(float strength)
        {
            _strength = enableHaptics ? Mathf.Clamp01(strength) : 0f;
        }

        private void Update()
        {
            if (_strength <= minStrength)
            {
                return;
            }

            if (!Application.isMobilePlatform && !enableInEditor)
            {
                return;
            }

            if (Time.unscaledTime < _nextPulseTime)
            {
                return;
            }

            TryVibrate();
            _nextPulseTime = Time.unscaledTime + Mathf.Lerp(
                weakestPulseInterval,
                strongestPulseInterval,
                _strength);
        }

        private static void TryVibrate()
        {
            Handheld.Vibrate();
        }

        private void OnDisable()
        {
            _strength = 0f;
        }
    }
}
