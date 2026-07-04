namespace ClimbGame.Climb3C.Feedback
{
    /// <summary>平台震动后端：把一次"震动 millis 毫秒、振幅 amplitude01(0..1)"落到具体平台。</summary>
    public interface IHapticBackend
    {
        bool SupportsAmplitude { get; }
        void Vibrate(long millis, float amplitude01);
        void Cancel();
    }
}
