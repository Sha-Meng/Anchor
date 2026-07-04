using UnityEngine;

namespace ClimbGame.Climb3C.Config
{
    /// <summary>
    /// RenderTexture 放大镜配置。RT 分辨率变更属"需重建"，其余即时生效。
    /// </summary>
    [CreateAssetMenu(fileName = "MagnifierConfig", menuName = "ClimbGame/Climb3C/Magnifier Config")]
    public sealed class MagnifierConfig : ScriptableObject
    {
        [Header("放大表现")]
        [Tooltip("放大倍率：副相机正交尺寸 = 基础尺寸 / 倍率，越大越近")]
        public float zoom = 2.5f;

        [Tooltip("副相机基础正交尺寸（世界单位半高）")]
        public float baseOrthoSize = 1.2f;

        [Header("屏幕显示")]
        [Tooltip("放大镜圆形显示直径（屏幕像素）")]
        public float screenDiameter = 220f;

        [Tooltip("放大镜相对触点的屏幕偏移，避免被手指盖住")]
        public Vector2 screenOffset = new Vector2(0f, 140f);

        [Header("RenderTexture（需重建）")]
        [Tooltip("RT 边长像素，移动端建议偏小以省性能")]
        public int renderTextureSize = 256;

        [Header("边框反馈")]
        [Tooltip("边框是否随震动档位变色")]
        public bool borderReactsToHaptic = true;

        public Color borderNone = new Color(0.7f, 0.7f, 0.7f, 0.9f);
        public Color borderSlight = new Color(1f, 0.85f, 0.1f, 0.95f);
        public Color borderIntense = new Color(1f, 0.2f, 0.15f, 1f);

        [Tooltip("边框厚度（屏幕像素）")]
        public float borderThickness = 6f;
    }
}
