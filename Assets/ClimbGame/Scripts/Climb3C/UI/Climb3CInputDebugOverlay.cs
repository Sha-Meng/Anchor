using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using ClimbGame.Climb3C.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace ClimbGame.Climb3C.UI
{
    /// <summary>
    /// 攀爬输入调试叠层：对比 raw 触点、偏移后有效触点、磁点目标与实际手位的屏幕/世界偏差。
    /// 同时驱动 <see cref="InputZoneOverlayUI"/> 的输入区与触点标记。
    /// </summary>
    public sealed class Climb3CInputDebugOverlay : MonoBehaviour
    {
        private ClimbTuningConfig _tuning;
        private Camera _camera;
        private ClimbController3D _controller;
        private InputZoneOverlayUI _zones;

        private DebugMarker _rawMarker;
        private DebugMarker _effectiveMarker;
        private DebugMarker _commandedMarker;
        private DebugMarker _handMarker;
        private RectTransform _lagLine;
        private Image _lagLineImage;
        private Text _ropeForceText;

        public void Build(Canvas canvas, ClimbTuningConfig tuning, Camera camera, ClimbController3D controller)
        {
            _tuning = tuning;
            _camera = camera;
            _controller = controller;

            _zones = gameObject.AddComponent<InputZoneOverlayUI>();
            _zones.Build(canvas, tuning);

            var root = canvas.transform;
            _rawMarker = CreateMarker(root, "DebugRawTouch", new Color(1f, 1f, 1f, 0.75f), 34f);
            _effectiveMarker = CreateMarker(root, "DebugEffectiveTouch", new Color(0.2f, 0.95f, 1f, 0.9f), 40f);
            _commandedMarker = CreateMarker(root, "DebugCommanded", new Color(0.35f, 1f, 0.45f, 0.95f), 22f, filled: true);
            _handMarker = CreateMarker(root, "DebugHand", new Color(1f, 0.88f, 0.2f, 0.95f), 18f, filled: true);

            var lineGo = new GameObject("DebugLagLine", typeof(RectTransform));
            _lagLine = lineGo.GetComponent<RectTransform>();
            _lagLine.SetParent(root, false);
            _lagLineImage = lineGo.AddComponent<Image>();
            _lagLineImage.color = new Color(1f, 0.45f, 0.2f, 0.85f);
            _lagLineImage.raycastTarget = false;
            _lagLine.sizeDelta = new Vector2(4f, 1f);
            _lagLine.gameObject.SetActive(false);

            _ropeForceText = CreateRopeForceText(root);
        }

        private void LateUpdate()
        {
            if (_controller == null || _tuning == null) return;

            _zones.SetActiveSide(_controller.CurrentHand);
            UpdateRopeForceText(_tuning.showInputDebug);

            bool showDebug = _tuning.showInputDebug;
            ClimbInputDebugSnapshot snap = _controller.InputDebug;

            if (!showDebug || !snap.Active)
            {
                SetMarkerVisible(_rawMarker, false);
                SetMarkerVisible(_effectiveMarker, false);
                SetMarkerVisible(_commandedMarker, false);
                SetMarkerVisible(_handMarker, false);
                _zones.SetTouchMarker(Vector2.zero, false);
                if (_lagLine != null) _lagLine.gameObject.SetActive(false);
                return;
            }

            bool showRaw = _tuning.showTouchMarker && _controller.CurrentHand != ClimbHand.None;
            SetMarkerVisible(_rawMarker, showRaw);
            if (showRaw) _rawMarker.Rect.position = snap.RawScreenPos;

            SetMarkerVisible(_effectiveMarker, true);
            _effectiveMarker.Rect.position = snap.EffectiveScreenPos;
            _zones.SetTouchMarker(snap.EffectiveScreenPos, true);

            Vector3 commandedScreen = WorldToScreen(snap.CommandedWorld);
            Vector3 handScreen = WorldToScreen(snap.ActualHandWorld);

            bool commandedOnScreen = commandedScreen.z > 0f;
            bool handOnScreen = handScreen.z > 0f;

            SetMarkerVisible(_commandedMarker, commandedOnScreen);
            if (commandedOnScreen) _commandedMarker.Rect.position = commandedScreen;

            SetMarkerVisible(_handMarker, handOnScreen);
            if (handOnScreen) _handMarker.Rect.position = handScreen;

            if (commandedOnScreen && handOnScreen)
            {
                UpdateLagLine((Vector2)commandedScreen, (Vector2)handScreen);
            }
            else if (_lagLine != null)
            {
                _lagLine.gameObject.SetActive(false);
            }

            DrawWorldDebug(snap);
        }

        private Vector3 WorldToScreen(Vector3 world)
        {
            if (_camera == null) return Vector3.zero;
            return _camera.WorldToScreenPoint(world);
        }

        private void DrawWorldDebug(ClimbInputDebugSnapshot snap)
        {
            Vector3 mapped = snap.MappedWorld;
            Vector3 commanded = snap.CommandedWorld;
            Vector3 hand = snap.ActualHandWorld;

            Debug.DrawLine(mapped, commanded, Color.magenta);
            Debug.DrawLine(commanded, hand, new Color(1f, 0.5f, 0.1f));
            Debug.DrawLine(snap.ReachStartHandWorld, hand, Color.cyan);
        }

        private void UpdateRopeForceText(bool visible)
        {
            if (_ropeForceText == null)
            {
                return;
            }

            _ropeForceText.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            var rope = _controller.RopeForceDebug;
            _ropeForceText.text =
                $"Rope Force: consuming={rope.Consuming} reason={rope.IgnoreReason}\n" +
                $"tension={rope.TensionStrength:0.00} constraint={rope.ConstraintDistance:0.00} " +
                $"fix={rope.SuggestedVelocityCorrection.magnitude:0.00}m/s";
        }

        private void UpdateLagLine(Vector2 from, Vector2 to)
        {
            if (_lagLine == null) return;

            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 2f)
            {
                _lagLine.gameObject.SetActive(false);
                return;
            }

            _lagLine.gameObject.SetActive(true);
            Vector2 mid = (from + to) * 0.5f;
            _lagLine.position = mid;
            _lagLine.sizeDelta = new Vector2(length, 4f);
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            _lagLine.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private static void SetMarkerVisible(DebugMarker marker, bool visible)
        {
            if (marker?.Rect == null) return;
            if (marker.Rect.gameObject.activeSelf != visible)
            {
                marker.Rect.gameObject.SetActive(visible);
            }
        }

        private static DebugMarker CreateMarker(Transform parent, string name, Color color, float size, bool filled = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = filled ? BuildDotSprite(64) : BuildRingSprite(96);
            img.color = color;
            img.raycastTarget = false;
            go.SetActive(false);
            return new DebugMarker(rt);
        }

        private static Text CreateRopeForceText(Transform parent)
        {
            var go = new GameObject("RopeForceDebugText", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(24f, 24f);
            rect.sizeDelta = new Vector2(720f, 72f);

            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.alignment = TextAnchor.LowerLeft;
            text.color = new Color(1f, 0.25f, 0.9f, 0.95f);
            text.raycastTarget = false;
            go.SetActive(false);
            return text;
        }

        private static Sprite BuildRingSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { name = "DebugTouchRing" };
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

        private static Sprite BuildDotSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.ARGB32, false) { name = "DebugTouchDot" };
            float r = size * 0.5f;
            Vector2 c = new Vector2(r, r);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                    float a = d <= r - 1f ? 1f : Mathf.Clamp01(r - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private sealed class DebugMarker
        {
            public readonly RectTransform Rect;
            public DebugMarker(RectTransform rect) => Rect = rect;
        }
    }
}
