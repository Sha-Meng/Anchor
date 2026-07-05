using ClimbGame.Climb3C.Config;
using UnityEngine;
using UnityEngine.UI;

namespace ClimbGame.Climb3C.UI
{
    /// <summary>
    /// 耐力圆环倒计时（uGUI）：跟随双手中心点 + 可配置世界偏移，投影到屏幕显示。
    /// 用径向填充（Radial360）表现耐力，随耐力下降圆环逐渐消退。
    /// </summary>
    public sealed class StaminaBarUI : MonoBehaviour
    {
        [SerializeField] private Color highColor = new Color(0.3f, 0.85f, 0.4f);
        [SerializeField] private Color lowColor = new Color(0.9f, 0.25f, 0.2f);
        [SerializeField] private bool visible = false;

        private RectTransform _root;
        private RectTransform _canvasRect;
        private Image _bg;
        private Image _fill;
        private Camera _camera;
        private Canvas _canvas;
        private StaminaConfig _config;
        private bool _hasWorldAnchor;
        private Vector3 _worldAnchor;
        private AudioClip _breathingClip;
        private AudioSource _breathingSource;
        private float _breathingVolume = 1f;
        private bool _isLowStamina;

        private static Sprite _ringSprite;

        public void Build(Canvas canvas, StaminaConfig config, Camera camera)
        {
            _canvas = canvas;
            _canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            _config = config;
            _camera = camera;

            float size = config != null ? config.ringSizePixels : 120f;
            float thickness = config != null ? config.ringThickness : 0.22f;

            var rootGo = new GameObject("StaminaRing", typeof(RectTransform));
            _root = rootGo.GetComponent<RectTransform>();
            _root.SetParent(canvas.transform, false);
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(size, size);

            Sprite ring = GetRingSprite(thickness);

            _bg = rootGo.AddComponent<Image>();
            _bg.sprite = ring;
            _bg.type = Image.Type.Simple;
            _bg.color = new Color(0f, 0f, 0f, 0.4f);

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.SetParent(_root, false);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            _fill = fillGo.AddComponent<Image>();
            _fill.sprite = ring;
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Radial360;
            _fill.fillOrigin = (int)Image.Origin360.Top;
            _fill.fillClockwise = false;
            _fill.fillAmount = 1f;
            _fill.color = highColor;

            SetVisible(visible);
        }

        public void SetVisible(bool v)
        {
            visible = v;
            if (_root != null) _root.gameObject.SetActive(v);
            UpdateBreathingLoop();
        }

        public void SetBreathingAudio(AudioClip clip, float volume = 1f)
        {
            _breathingClip = clip;
            _breathingVolume = Mathf.Clamp01(volume);
            UpdateBreathingLoop();
        }

        /// <summary>设置圆环跟随的世界锚点（双手中心点，偏移在内部按配置叠加）。</summary>
        public void SetWorldAnchor(Vector3 worldMidpoint)
        {
            _worldAnchor = worldMidpoint;
            _hasWorldAnchor = true;
        }

        public void SetRatio(float ratio)
        {
            if (_fill == null) return;
            ratio = Mathf.Clamp01(ratio);
            _fill.fillAmount = ratio;
            // 透明度恒定；耐力降到 1/3 及以下才变红，否则用正常色
            _isLowStamina = ratio <= 1f / 3f;
            Color c = _isLowStamina ? lowColor : highColor;
            c.a = 1f;
            _fill.color = c;
            UpdateBreathingLoop();
        }

        private void UpdateBreathingLoop()
        {
            bool shouldPlay = visible && _isLowStamina && _breathingClip != null;
            if (!shouldPlay)
            {
                if (_breathingSource != null && _breathingSource.isPlaying)
                {
                    _breathingSource.Stop();
                }
                return;
            }

            AudioSource source = EnsureBreathingSource();
            if (source.clip != _breathingClip)
            {
                source.clip = _breathingClip;
            }

            source.volume = _breathingVolume;
            source.loop = true;
            source.playOnAwake = false;
            if (!source.isPlaying)
            {
                source.Play();
            }
        }

        private AudioSource EnsureBreathingSource()
        {
            if (_breathingSource == null)
            {
                _breathingSource = gameObject.AddComponent<AudioSource>();
                _breathingSource.spatialBlend = 0f;
            }

            return _breathingSource;
        }

        private void LateUpdate()
        {
            if (!visible || _root == null || _camera == null || !_hasWorldAnchor) return;

            // 半径/尺寸每帧从配置应用，支持运行时实时调节
            if (_config != null)
            {
                float size = Mathf.Max(1f, _config.ringSizePixels);
                if (_root.sizeDelta.x != size) _root.sizeDelta = new Vector2(size, size);
            }

            Vector3 world = _worldAnchor + (_config != null ? _config.ringWorldOffset : Vector3.up * 0.5f);
            Vector3 screen = _camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                // 锚点在相机背后，隐藏
                if (_root.gameObject.activeSelf) _root.gameObject.SetActive(false);
                return;
            }
            if (!_root.gameObject.activeSelf) _root.gameObject.SetActive(true);

            // 屏幕像素须换算到 Canvas 局部坐标，才能与 CanvasScaler 下的 sizeDelta 一致（手机/PC 表现对齐）
            if (_canvasRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    new Vector2(screen.x, screen.y),
                    _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null,
                    out Vector2 localPoint))
            {
                _root.anchoredPosition = localPoint;
            }
        }

        /// <summary>运行时生成一张环形（donut）贴图，供背景与填充复用。</summary>
        private static Sprite GetRingSprite(float thickness)
        {
            if (_ringSprite != null) return _ringSprite;

            const int res = 128;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float center = (res - 1) * 0.5f;
            float outer = center;
            float inner = outer * (1f - Mathf.Clamp(thickness, 0.05f, 0.9f));
            var pixels = new Color32[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // 环带内不透明，边缘做 1px 抗锯齿
                    float aOuter = Mathf.Clamp01(outer - d);
                    float aInner = Mathf.Clamp01(d - inner);
                    byte a = (byte)(Mathf.Clamp01(Mathf.Min(aOuter, aInner)) * 255f);
                    pixels[y * res + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f);
            return _ringSprite;
        }
    }
}
