using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ClimbGame.Climb3C.UI
{
    /// <summary>
    /// 屏幕左右输入区的可视化：用半透明色块标出左右区，并高亮"当前该攀爬的手"所在的一侧。
    /// 另外可显示一个当前触点的小标记，方便 PC 用鼠标调试。
    /// </summary>
    public sealed class InputZoneOverlayUI : MonoBehaviour
    {
        private ClimbTuningConfig _tuning;
        private RectTransform _left;
        private RectTransform _right;
        private Image _leftImg;
        private Image _rightImg;
        private RectTransform _marker;
        private Image _markerImg;

        private ClimbHand _activeSide = ClimbHand.None;

        public void Build(Canvas canvas, ClimbTuningConfig tuning)
        {
            _tuning = tuning;

            _left = CreatePanel(canvas.transform, "LeftInputZone", out _leftImg);
            _right = CreatePanel(canvas.transform, "RightInputZone", out _rightImg);

            var markerGo = new GameObject("TouchMarker", typeof(RectTransform));
            _marker = markerGo.GetComponent<RectTransform>();
            _marker.SetParent(canvas.transform, false);
            _marker.sizeDelta = new Vector2(46f, 46f);
            _markerImg = markerGo.AddComponent<Image>();
            _markerImg.sprite = BuildRingSprite(96);
            _markerImg.color = new Color(1f, 1f, 1f, 0.85f);
            _marker.gameObject.SetActive(false);

            // 保证色块在最底层，不遮挡耐力条/放大镜（后创建的在上层）
            _left.SetAsFirstSibling();
            _right.SetAsFirstSibling();
        }

        private static RectTransform CreatePanel(Transform parent, string name, out Image img)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            img = go.AddComponent<Image>();
            img.raycastTarget = false;
            return rt;
        }

        public void SetActiveSide(ClimbHand active) => _activeSide = active;

        public void SetTouchMarker(Vector2 screenPos, bool visible)
        {
            if (_marker == null) return;
            bool show = visible && _tuning != null && _tuning.showTouchMarker;
            if (_marker.gameObject.activeSelf != show) _marker.gameObject.SetActive(show);
            if (show) _marker.position = screenPos;
        }

        private void Update()
        {
            if (_tuning == null || _left == null) return;

            bool show = _tuning.showInputZones;
            if (_left.gameObject.activeSelf != show) _left.gameObject.SetActive(show);
            if (_right.gameObject.activeSelf != show) _right.gameObject.SetActive(show);
            if (!show) return;

            float split = _tuning.zoneSplit;
            float h = _tuning.zoneHorizontalInset;
            float b = _tuning.zoneBottomInset;
            float t = 1f - _tuning.zoneTopInset;

            ApplyAnchors(_left, h, b, split, t);
            ApplyAnchors(_right, split, b, 1f - h, t);

            _leftImg.color = ColorFor(_tuning.leftZoneColor, ClimbHand.Left);
            _rightImg.color = ColorFor(_tuning.rightZoneColor, ClimbHand.Right);
        }

        private Color ColorFor(Color baseColor, ClimbHand side)
        {
            bool active = _activeSide == ClimbHand.None || _activeSide == side;
            float a = active ? _tuning.activeZoneAlpha : _tuning.inactiveZoneAlpha;
            return new Color(baseColor.r, baseColor.g, baseColor.b, a);
        }

        private static void ApplyAnchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Sprite BuildRingSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { name = "TouchRing" };
            float r = size * 0.5f;
            Vector2 c = new Vector2(r, r);
            float inner = r * 0.62f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = (d <= r - 1f && d >= inner) ? 1f : Mathf.Clamp01(Mathf.Min(r - d, d - inner + 1f));
                    if (d > r || d < inner - 1f) a = 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                }
            }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
