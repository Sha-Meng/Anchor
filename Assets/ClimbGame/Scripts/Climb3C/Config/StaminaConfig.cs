using UnityEngine;

namespace ClimbGame.Climb3C.Config
{
    /// <summary>
    /// 耐力条配置。全部即时生效。
    /// </summary>
    [CreateAssetMenu(fileName = "StaminaConfig", menuName = "ClimbGame/Climb3C/Stamina Config")]
    public sealed class StaminaConfig : ScriptableObject
    {
        [Header("耐力总量")]
        [Tooltip("耐力上限")]
        public float maxStamina = 100f;

        [Header("消耗")]
        [Tooltip("伸手/悬挂（未稳定抓在铆钉上）时每秒消耗")]
        public float drainPerSecond = 12f;

        [Tooltip("放弃归位过程中的额外消耗系数（相对 drainPerSecond）")]
        [Range(0f, 2f)] public float abandonDrainMultiplier = 1f;

        [Header("恢复")]
        [Tooltip("稳定抓在铆钉上时每秒恢复（快速）")]
        public float recoverPerSecond = 45f;

        [Header("摔落")]
        [Tooltip("耐力归零后固定下落距离（世界单位）")]
        public float fallDistance = 2.2f;

        [Tooltip("摔落落定后恢复到的耐力比例")]
        [Range(0f, 1f)] public float recoverOnLandRatio = 0.6f;

        [Tooltip("摔落后自由物理时长上限（秒），超时强制吸附最近铆钉")]
        public float maxFallSeconds = 1.5f;

        [Header("耐力圆环 UI（跟随双手中心 + 偏移）")]
        [Tooltip("圆环相对双手中心点的世界偏移（x 右、y 上、z 前后）")]
        public Vector3 ringWorldOffset = new Vector3(0f, 0.5f, 0f);

        [Tooltip("圆环直径（参考分辨率下的像素）")]
        public float ringSizePixels = 120f;

        [Tooltip("圆环厚度占半径的比例（0~1，越大越粗）")]
        [Range(0.05f, 0.9f)] public float ringThickness = 0.22f;
    }
}
