using ClimbGame.Inputs;
using UnityEngine;

namespace ClimbGame.Character
{
    /// <summary>
    /// Moves the climber across a 2D plane based on an <see cref="IClimbInput"/> source.
    /// Climbing here means free 2-axis movement over a wall (no gravity), clamped to a
    /// rectangular climb area. It exposes its state through <see cref="IClimberMotion"/>
    /// and knows nothing about which device drives it or how it is animated.
    /// </summary>
    [AddComponentMenu("ClimbGame/Climber Controller")]
    public sealed class ClimberController : MonoBehaviour, IClimberMotion
    {
        [Header("Movement")]
        [Tooltip("Max climb speed in world units per second.")]
        [SerializeField] private float moveSpeed = 3.5f;
        [Tooltip("Higher = snappier response, lower = more inertia.")]
        [SerializeField] private float responsiveness = 12f;
        [Range(0f, 0.9f)]
        [SerializeField] private float deadZone = 0.15f;

        [Header("Climb area (world units)")]
        [SerializeField] private Rect climbArea = new Rect(-3.5f, -4f, 7f, 8f);

        private IClimbInput _input;
        private Vector2 _velocity;
        private ClimbState _state;
        private int _facing = 1;

        // --- Composition-root wiring (keeps interface fields out of the inspector) ---
        public void SetInput(IClimbInput input) => _input = input;

        public void SetClimbArea(Rect area) => climbArea = area;

        public void SetMoveSpeed(float speed) => moveSpeed = Mathf.Max(0.01f, speed);

        // --- IClimberMotion ---
        public ClimbState State => _state;
        public Vector2 Velocity => _velocity;
        public float ClimbSpeed01 => Mathf.Clamp01(_velocity.magnitude / Mathf.Max(0.001f, moveSpeed));
        public int Facing => _facing;

        private void Update()
        {
            Vector2 intent = _input != null ? _input.Direction : Vector2.zero;
            if (intent.magnitude < deadZone) intent = Vector2.zero;

            Vector2 targetVelocity = intent * moveSpeed;
            // Framerate-independent smoothing toward the target velocity.
            float t = 1f - Mathf.Exp(-responsiveness * Time.deltaTime);
            _velocity = Vector2.Lerp(_velocity, targetVelocity, t);

            Vector3 next = transform.position + (Vector3)(_velocity * Time.deltaTime);
            next.x = Mathf.Clamp(next.x, climbArea.xMin, climbArea.xMax);
            next.y = Mathf.Clamp(next.y, climbArea.yMin, climbArea.yMax);
            transform.position = next;

            if (Mathf.Abs(intent.x) > 0.01f)
                _facing = intent.x > 0f ? 1 : -1;

            _state = _velocity.magnitude > 0.05f ? ClimbState.Climbing : ClimbState.Idle;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
            Gizmos.DrawWireCube(climbArea.center, new Vector3(climbArea.width, climbArea.height, 0f));
        }
    }
}
