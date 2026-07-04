using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ClimbGame.Climb3C.Feedback
{
    /// <summary>
    /// RenderTexture 放大镜：一台正交副相机对准当前攀爬手所在墙面区域，渲染到 RT，
    /// 以圆形遮罩显示在触点附近的屏幕位置，边框可随震动档位变色。
    /// </summary>
    public sealed class HandMagnifier : MonoBehaviour
    {
        private MagnifierConfig _config;
        private Camera _sourceCamera;
        private Camera _lensCamera;
        private RenderTexture _rt;

        private Canvas _canvas;
        private RectTransform _root;
        private Image _border;
        private RawImage _view;
        private Sprite _circle;

        private bool _visible;

        public void Setup(MagnifierConfig config, Camera sourceCamera, Canvas canvas, int uiLayer)
        {
            _config = config;
            _sourceCamera = sourceCamera;
            _canvas = canvas;

            int size = Mathf.Clamp(config != null ? config.renderTextureSize : 256, 64, 1024);
            _rt = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32) { name = "MagnifierRT" };

            var camGo = new GameObject("MagnifierLensCamera");
            camGo.transform.SetParent(transform, false);
            _lensCamera = camGo.AddComponent<Camera>();
            _lensCamera.orthographic = true;
            _lensCamera.targetTexture = _rt;
            _lensCamera.clearFlags = CameraClearFlags.SolidColor;
            _lensCamera.backgroundColor = new Color(0.1f, 0.1f, 0.12f, 1f);
            _lensCamera.cullingMask = ~(1 << uiLayer);
            _lensCamera.enabled = false;

            _circle = BuildCircleSprite(128);
            BuildUi();
            SetVisible(false);
        }

        private void BuildUi()
        {
            var rootGo = new GameObject("Magnifier", typeof(RectTransform));
            _root = rootGo.GetComponent<RectTransform>();
            _root.SetParent(_canvas.transform, false);
            _root.sizeDelta = new Vector2(1f, 1f);

            var borderGo = new GameObject("Border", typeof(RectTransform));
            _border = borderGo.AddComponent<Image>();
            _border.sprite = _circle;
            _border.type = Image.Type.Simple;
            var borderRt = _border.rectTransform;
            borderRt.SetParent(_root, false);

            var maskGo = new GameObject("Mask", typeof(RectTransform));
            var maskImg = maskGo.AddComponent<Image>();
            maskImg.sprite = _circle;
            var mask = maskGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            var maskRt = maskImg.rectTransform;
            maskRt.SetParent(_root, false);

            var viewGo = new GameObject("View", typeof(RectTransform));
            _view = viewGo.AddComponent<RawImage>();
            _view.texture = _rt;
            _view.rectTransform.SetParent(maskRt, false);
        }

        /// <summary>更新放大镜：worldFocus 手部世界点，screenPos 触点屏幕位置，tier 震动档位。</summary>
        public void UpdateLens(Vector3 worldFocus, Vector2 screenPos, HapticTier tier)
        {
            if (_config == null || _sourceCamera == null) return;
            if (!_visible) SetVisible(true);

            _lensCamera.transform.rotation = _sourceCamera.transform.rotation;
            float dist = 5f;
            _lensCamera.transform.position = worldFocus - _sourceCamera.transform.forward * dist;
            _lensCamera.nearClipPlane = 0.01f;
            _lensCamera.farClipPlane = dist + 5f;
            _lensCamera.orthographicSize = Mathf.Max(0.05f, _config.baseOrthoSize / Mathf.Max(0.1f, _config.zoom));
            _lensCamera.Render();

            float d = _config.screenDiameter;
            Vector2 pos = screenPos + _config.screenOffset;
            // 转到 canvas 局部坐标（Overlay 模式下屏幕坐标≈局部坐标，仍做换算以兼容缩放）
            _root.position = pos;

            _border.rectTransform.sizeDelta = new Vector2(d + _config.borderThickness * 2f, d + _config.borderThickness * 2f);
            (_border.rectTransform).anchoredPosition = Vector2.zero;

            var maskRt = _view.rectTransform.parent as RectTransform;
            if (maskRt != null)
            {
                maskRt.sizeDelta = new Vector2(d, d);
                maskRt.anchoredPosition = Vector2.zero;
            }
            _view.rectTransform.sizeDelta = new Vector2(d, d);
            _view.rectTransform.anchoredPosition = Vector2.zero;

            _border.color = BorderColor(tier);
        }

        public void Hide()
        {
            if (_visible) SetVisible(false);
        }

        private void SetVisible(bool v)
        {
            _visible = v;
            if (_root != null) _root.gameObject.SetActive(v);
        }

        private Color BorderColor(HapticTier tier)
        {
            if (_config == null || !_config.borderReactsToHaptic) return _config != null ? _config.borderNone : Color.white;
            switch (tier)
            {
                case HapticTier.Intense: return _config.borderIntense;
                case HapticTier.Slight: return _config.borderSlight;
                default: return _config.borderNone;
            }
        }

        private static Sprite BuildCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { name = "CircleMask" };
            float r = size * 0.5f;
            Vector2 c = new Vector2(r, r);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dd = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = dd <= r - 1f ? 1f : Mathf.Clamp01(r - dd);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private void OnDestroy()
        {
            if (_rt != null) _rt.Release();
        }
    }
}
