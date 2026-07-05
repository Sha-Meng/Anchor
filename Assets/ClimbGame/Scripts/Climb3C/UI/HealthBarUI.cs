using Anchor.RivetRopeSystem;
using UnityEngine;
using UnityEngine.UI;

namespace ClimbGame.Climb3C.UI
{
    public sealed class HealthBarUI : MonoBehaviour
    {
        [SerializeField] private Vector2 anchor = new Vector2(0f, 1f);
        [SerializeField] private Vector2 pivot = new Vector2(0f, 1f);
        [SerializeField] private Vector2 offset = new Vector2(28f, -28f);
        [SerializeField] private Vector2 size = new Vector2(260f, 28f);
        [SerializeField] private Color highColor = new Color(0.25f, 0.85f, 0.35f);
        [SerializeField] private Color lowColor = new Color(0.95f, 0.2f, 0.18f);

        private RectTransform _root;
        private RectTransform _fillRect;
        private Image _fill;
        private Text _label;

        public void Build(Canvas canvas)
        {
            var rootGo = new GameObject("HealthBar", typeof(RectTransform));
            _root = rootGo.GetComponent<RectTransform>();
            _root.SetParent(canvas.transform, false);
            _root.anchorMin = anchor;
            _root.anchorMax = anchor;
            _root.pivot = pivot;
            _root.anchoredPosition = offset;
            _root.sizeDelta = size;

            var background = rootGo.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            var fillRect = fillGo.GetComponent<RectTransform>();
            _fillRect = fillRect;
            fillRect.SetParent(_root, false);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            _fill = fillGo.AddComponent<Image>();
            _fill.type = Image.Type.Simple;
            _fill.color = highColor;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.SetParent(_root, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            _label = labelGo.AddComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.fontSize = 17;
            _label.color = Color.white;
            _label.alignment = TextAnchor.MiddleCenter;
        }

        public void Refresh(PlayerHealthSnapshot snapshot)
        {
            var ratio = snapshot.HealthRatio;
            if (_fillRect != null)
            {
                _fillRect.localScale = new Vector3(ratio, 1f, 1f);
            }

            if (_fill != null)
            {
                _fill.color = ratio <= 0.33f ? lowColor : highColor;
            }

            if (_label != null)
            {
                _label.text = $"生命 {snapshot.CurrentHealth:0}/{snapshot.MaxHealth:0}";
            }
        }
    }
}
