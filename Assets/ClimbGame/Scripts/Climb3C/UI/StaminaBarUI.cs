using UnityEngine;
using UnityEngine.UI;

namespace ClimbGame.Climb3C.UI
{
    /// <summary>屏幕顶部耐力条（uGUI）。可配置显隐与颜色。</summary>
    public sealed class StaminaBarUI : MonoBehaviour
    {
        private Image _fill;
        private RectTransform _fillRect;
        private float _fullWidth;

        [SerializeField] private Color highColor = new Color(0.3f, 0.85f, 0.4f);
        [SerializeField] private Color lowColor = new Color(0.9f, 0.25f, 0.2f);
        [SerializeField] private bool visible = true;

        public void Build(Canvas canvas)
        {
            var rootGo = new GameObject("StaminaBar", typeof(RectTransform));
            var root = rootGo.GetComponent<RectTransform>();
            root.SetParent(canvas.transform, false);
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -24f);
            _fullWidth = 420f;
            root.sizeDelta = new Vector2(_fullWidth, 26f);

            var bg = rootGo.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.5f);

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            _fillRect = fillGo.GetComponent<RectTransform>();
            _fillRect.SetParent(root, false);
            _fillRect.anchorMin = new Vector2(0f, 0f);
            _fillRect.anchorMax = new Vector2(0f, 1f);
            _fillRect.pivot = new Vector2(0f, 0.5f);
            _fillRect.offsetMin = new Vector2(3f, 3f);
            _fillRect.offsetMax = new Vector2(0f, -3f);
            _fillRect.sizeDelta = new Vector2(_fullWidth - 6f, 0f);
            _fill = fillGo.AddComponent<Image>();
            _fill.color = highColor;

            SetVisible(visible);
        }

        public void SetVisible(bool v)
        {
            visible = v;
            gameObject.SetActive(true);
            if (_fillRect != null) _fillRect.parent.gameObject.SetActive(v);
        }

        public void SetRatio(float ratio)
        {
            if (_fillRect == null) return;
            ratio = Mathf.Clamp01(ratio);
            _fillRect.sizeDelta = new Vector2((_fullWidth - 6f) * ratio, _fillRect.sizeDelta.y);
            _fill.color = Color.Lerp(lowColor, highColor, ratio);
        }
    }
}
