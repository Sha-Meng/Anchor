using UnityEngine;

namespace ClimbGame.Climb3C.Feedback
{
    /// <summary>编辑器/PC：无真机震动，仅记录状态供可视化回退。</summary>
    public sealed class NullHapticBackend : IHapticBackend
    {
        public bool SupportsAmplitude => false;
        public void Vibrate(long millis, float amplitude01) { }
        public void Cancel() { }
    }

    /// <summary>
    /// 通用移动端后端（iOS 及不支持振幅的 Android 回退）：使用 Handheld.Vibrate 的开/关震动，
    /// 通过上层的脉冲频率调制来表达"越近越强"。振幅参数被忽略。
    /// </summary>
    public sealed class SimpleHandheldBackend : IHapticBackend
    {
        public bool SupportsAmplitude => false;

        public void Vibrate(long millis, float amplitude01)
        {
#if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
#endif
        }

        public void Cancel() { }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>Android 振动器：API 26+ 支持振幅（VibrationEffect.createOneShot）。</summary>
    public sealed class AndroidHapticBackend : IHapticBackend
    {
        private AndroidJavaObject _vibrator;
        private int _sdkInt;
        private bool _hasAmplitudeControl;

        public bool SupportsAmplitude => _hasAmplitudeControl;

        public AndroidHapticBackend()
        {
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    _sdkInt = version.GetStatic<int>("SDK_INT");
                }
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }
                if (_vibrator != null && _sdkInt >= 26)
                {
                    _hasAmplitudeControl = _vibrator.Call<bool>("hasAmplitudeControl");
                }
            }
            catch
            {
                _vibrator = null;
                _hasAmplitudeControl = false;
            }
        }

        public void Vibrate(long millis, float amplitude01)
        {
            if (_vibrator == null || millis <= 0) return;
            try
            {
                if (_sdkInt >= 26)
                {
                    int amp = Mathf.Clamp(Mathf.RoundToInt(amplitude01 * 255f), 1, 255);
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    {
                        int useAmp = _hasAmplitudeControl ? amp : -1; // -1 = DEFAULT_AMPLITUDE
                        AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
                            "createOneShot", millis, useAmp);
                        _vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    _vibrator.Call("vibrate", millis);
                }
            }
            catch
            {
                // 忽略平台异常，遇到具体机型问题再处理
            }
        }

        public void Cancel()
        {
            try { _vibrator?.Call("cancel"); }
            catch { }
        }
    }
#endif

    public static class HapticBackendFactory
    {
        public static IHapticBackend Create()
        {
#if UNITY_EDITOR
            return new NullHapticBackend();
#elif UNITY_ANDROID
            return new AndroidHapticBackend();
#elif UNITY_IOS
            return new SimpleHandheldBackend();
#else
            return new NullHapticBackend();
#endif
        }
    }
}
