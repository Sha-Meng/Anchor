using UnityEngine;
using UnityEngine.EventSystems;

namespace ClimbGame.Inputs
{
    /// <summary>
    /// On-screen virtual joystick (uGUI). Drag the handle to steer the climb.
    /// Works with touch on device and with the mouse in the Editor / standalone.
    /// Exposes its state through <see cref="IClimbInput"/> so it is a drop-in source.
    /// </summary>
    [AddComponentMenu("ClimbGame/Input/Virtual Joystick")]
    public sealed class VirtualJoystick : MonoBehaviour, IClimbInput,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [Tooltip("Max handle travel from the centre, in the background's local units.")]
        [SerializeField] private float travelRadius = 60f;
        [Range(0f, 0.9f)]
        [SerializeField] private float deadZone = 0.08f;

        private Vector2 _direction;
        private Canvas _canvas;

        public Vector2 Direction => _direction;

        public void Configure(RectTransform backgroundRect, RectTransform handleRect, float radius)
        {
            background = backgroundRect;
            handle = handleRect;
            travelRadius = radius;
        }

        private void Awake()
        {
            if (background == null) background = transform as RectTransform;
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null) return;

            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();

            Camera cam = null;
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = _canvas.worldCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, eventData.position, cam, out Vector2 local))
                return;

            Vector2 clamped = Vector2.ClampMagnitude(local, travelRadius);
            if (handle != null) handle.anchoredPosition = clamped;

            Vector2 raw = clamped / travelRadius;
            _direction = raw.magnitude < deadZone ? Vector2.zero : raw;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _direction = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }
    }
}
