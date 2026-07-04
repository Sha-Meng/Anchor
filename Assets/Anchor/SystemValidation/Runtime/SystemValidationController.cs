using Anchor.ForceSystem;
using Anchor.LevelAnchorSystem;
using UnityEngine;

namespace Anchor.SystemValidation
{
    [DisallowMultipleComponent]
    public sealed class SystemValidationController : MonoBehaviour, IForceInputAdapter
    {
        [Header("Input Projection")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private LayerMask wallMask = ~0;
        [SerializeField] private float maxRayDistance = 200f;
        [SerializeField] private float markerHoverHeight = 0.08f;

        [Header("Query / Force")]
        [SerializeField] private MonoBehaviour gripQueryProviderSource;
        [SerializeField] private MonoBehaviour levelAnchorQuerySource;
        [SerializeField] private ForceSystemConfig forceConfig;
        [SerializeField] private float gripQueryRadius = 0.25f;
        [SerializeField] private float nearestAnchorSearchDistance = 10f;
        [SerializeField] private float defaultStamina = 1f;

        [Header("Feedback")]
        [SerializeField] private Transform leftMarker;
        [SerializeField] private Transform rightMarker;
        [SerializeField] private SystemValidationDebugPanel debugPanel;
        [SerializeField] private MobileHapticFeedbackAdapter haptics;
        [SerializeField] private bool drawGizmos = true;

        private IGripQueryProvider _gripQueryProvider;
        private ILevelAnchorQuery _levelAnchorQuery;
        private LevelAnchorRegistry _registry;
        private SystemValidationLevelAnchorRegistry _validationRegistry;
        private ValidationHandInputState _leftHand;
        private ValidationHandInputState _rightHand;
        private ForceEvaluationMemory _memory = ForceEvaluationMemory.CreateDefault();
        private ForceEvaluationResult _lastResult;
        private ForceEvaluationSettings _lastSettings;
        private ForceState _lastObservedState = ForceState.Stable;
        private float _lastStateChangeTime;

        public ValidationHandInputState LeftHand => _leftHand;
        public ValidationHandInputState RightHand => _rightHand;
        public ForceEvaluationResult LastResult => _lastResult;
        public ForceEvaluationSettings LastSettings => _lastSettings;
        public float LastStateChangeTime => _lastStateChangeTime;
        public int RegisteredAnchorCount => _validationRegistry != null
            ? _validationRegistry.RegisteredCount
            : _registry != null ? _registry.RegisteredCount : 0;
        public int SkippedAnchorCount => _validationRegistry != null
            ? _validationRegistry.SkippedCount
            : _registry != null ? _registry.SkippedCount : 0;

        private void Awake()
        {
            _leftHand.Side = ValidationHandSide.Left;
            _rightHand.Side = ValidationHandSide.Right;
            ResolveInterfaces();
        }

        private void OnEnable()
        {
            _memory = ForceEvaluationMemory.CreateDefault();
            _lastObservedState = ForceState.Stable;
            _lastStateChangeTime = Time.time;
            ResolveInterfaces();
        }

        private void OnDisable()
        {
            if (haptics != null)
            {
                haptics.SetStrength(0f);
            }
        }

        private void Update()
        {
            ResolveInterfaces();
            UpdateInputPoints();
            QueryHands();
            EvaluateCurrentFrame(Time.deltaTime);

            if (debugPanel != null)
            {
                debugPanel.UpdateFrom(this);
            }

            if (haptics != null)
            {
                haptics.SetStrength(ResolveHapticStrength());
            }
        }

        public ForceEvaluationInput BuildInput(float deltaTime)
        {
            return new ForceEvaluationInput
            {
                LeftHand = HandForceInput.FromGrip(
                    _leftHand.IsTouching,
                    _leftHand.Grip,
                    defaultStamina,
                    _leftHand.WorldPosition),
                RightHand = HandForceInput.FromGrip(
                    _rightHand.IsTouching,
                    _rightHand.Grip,
                    defaultStamina,
                    _rightHand.WorldPosition),
                Body = default,
                PreviousState = _memory.PreviousState,
                DeltaTime = deltaTime
            };
        }

        public void ResetEvaluation()
        {
            _memory.Reset();
            _lastResult = default;
            _lastObservedState = ForceState.Stable;
            _lastStateChangeTime = Time.time;
        }

        private void ResolveInterfaces()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            _gripQueryProvider = gripQueryProviderSource as IGripQueryProvider;
            _levelAnchorQuery = levelAnchorQuerySource as ILevelAnchorQuery;

            if (_levelAnchorQuery == null)
            {
                _levelAnchorQuery = gripQueryProviderSource as ILevelAnchorQuery;
            }

            _registry = levelAnchorQuerySource as LevelAnchorRegistry;
            if (_registry == null)
            {
                _registry = gripQueryProviderSource as LevelAnchorRegistry;
            }

            _validationRegistry = levelAnchorQuerySource as SystemValidationLevelAnchorRegistry;
            if (_validationRegistry == null)
            {
                _validationRegistry = gripQueryProviderSource as SystemValidationLevelAnchorRegistry;
            }
        }

        private void UpdateInputPoints()
        {
            if (targetCamera == null)
            {
                _leftHand.Clear(Vector2.zero);
                _rightHand.Clear(Vector2.zero);
                SetMarker(leftMarker, _leftHand);
                SetMarker(rightMarker, _rightHand);
                return;
            }

            if (Input.touchCount > 0)
            {
                UpdateTouchInput();
                return;
            }

            UpdateMouseInput();
        }

        private void UpdateMouseInput()
        {
            var screenPosition = (Vector2)Input.mousePosition;
            if (Input.GetMouseButton(0))
            {
                UpdateHandFromScreen(ref _leftHand, screenPosition);
            }
            else
            {
                _leftHand.Clear(screenPosition);
            }

            if (Input.GetMouseButton(1))
            {
                UpdateHandFromScreen(ref _rightHand, screenPosition);
            }
            else
            {
                _rightHand.Clear(screenPosition);
            }

            SetMarker(leftMarker, _leftHand);
            SetMarker(rightMarker, _rightHand);
        }

        private void UpdateTouchInput()
        {
            var sawLeft = false;
            var sawRight = false;
            var lastLeftPosition = _leftHand.ScreenPosition;
            var lastRightPosition = _rightHand.ScreenPosition;

            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                var side = touch.position.x < Screen.width * 0.5f
                    ? ValidationHandSide.Left
                    : ValidationHandSide.Right;

                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    if (side == ValidationHandSide.Left)
                    {
                        _leftHand.Clear(touch.position);
                        sawLeft = true;
                    }
                    else
                    {
                        _rightHand.Clear(touch.position);
                        sawRight = true;
                    }

                    continue;
                }

                if (side == ValidationHandSide.Left)
                {
                    sawLeft = true;
                    lastLeftPosition = touch.position;
                    UpdateHandFromScreen(ref _leftHand, touch.position);
                }
                else
                {
                    sawRight = true;
                    lastRightPosition = touch.position;
                    UpdateHandFromScreen(ref _rightHand, touch.position);
                }
            }

            if (!sawLeft)
            {
                _leftHand.Clear(lastLeftPosition);
            }

            if (!sawRight)
            {
                _rightHand.Clear(lastRightPosition);
            }

            SetMarker(leftMarker, _leftHand);
            SetMarker(rightMarker, _rightHand);
        }

        private void UpdateHandFromScreen(ref ValidationHandInputState hand, Vector2 screenPosition)
        {
            var ray = targetCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out var hit, maxRayDistance, wallMask, QueryTriggerInteraction.Ignore))
            {
                hand.SetHit(screenPosition, hit.point, hit.normal);
                return;
            }

            hand.Clear(screenPosition);
        }

        private void SetMarker(Transform marker, ValidationHandInputState hand)
        {
            if (marker == null)
            {
                return;
            }

            marker.gameObject.SetActive(hand.IsTouching);
            if (hand.IsTouching)
            {
                marker.position = hand.WorldPosition + hand.SurfaceNormal * markerHoverHeight;
            }
        }

        private void QueryHands()
        {
            QueryHand(ref _leftHand);
            QueryHand(ref _rightHand);
        }

        private void QueryHand(ref ValidationHandInputState hand)
        {
            if (!hand.IsTouching)
            {
                hand.Grip = GripQueryResult.None();
                hand.NearestAnchor = AnchorPointQueryResult.None(hand.WorldPosition);
                return;
            }

            if (_levelAnchorQuery != null &&
                _levelAnchorQuery.TryFindNearestAnchor(hand.WorldPosition, out var nearest, nearestAnchorSearchDistance))
            {
                hand.NearestAnchor = nearest;
            }
            else
            {
                hand.NearestAnchor = AnchorPointQueryResult.None(hand.WorldPosition);
            }

            if (_gripQueryProvider != null &&
                _gripQueryProvider.TryQueryGrip(hand.WorldPosition, gripQueryRadius, out var grip))
            {
                hand.Grip = grip;
            }
            else
            {
                hand.Grip = GripQueryResult.None(hand.SurfaceNormal);
            }
        }

        private void EvaluateCurrentFrame(float deltaTime)
        {
            _lastSettings = forceConfig != null ? forceConfig.Settings : ForceEvaluationSettings.CreateDefault();
            var previousState = _lastResult.State;
            var input = BuildInput(deltaTime);
            _lastResult = ForceEvaluator.Evaluate(input, ref _memory, _lastSettings);

            if (_lastResult.State != previousState || _lastResult.State != _lastObservedState)
            {
                _lastObservedState = _lastResult.State;
                _lastStateChangeTime = Time.time;
            }
        }

        private float ResolveHapticStrength()
        {
            var strength = 0f;
            if (_leftHand.IsTouching && _lastResult.LeftHand.IsEffective)
            {
                strength = Mathf.Max(strength, _lastResult.LeftHand.GripQuality);
            }

            if (_rightHand.IsTouching && _lastResult.RightHand.IsEffective)
            {
                strength = Mathf.Max(strength, _lastResult.RightHand.GripQuality);
            }

            return strength;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos)
            {
                return;
            }

            DrawHandGizmo(_leftHand, Color.cyan);
            DrawHandGizmo(_rightHand, new Color(1f, 0.45f, 0.1f));
        }

        private static void DrawHandGizmo(ValidationHandInputState hand, Color color)
        {
            if (!hand.HasValidWorldPosition)
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawWireSphere(hand.WorldPosition, 0.18f);

            if (hand.NearestAnchor.Found)
            {
                Gizmos.DrawLine(hand.WorldPosition, hand.NearestAnchor.WorldPosition);
                Gizmos.DrawWireSphere(hand.NearestAnchor.WorldPosition, Mathf.Max(0.05f, hand.NearestAnchor.GrabRadius));
            }
        }
    }
}
