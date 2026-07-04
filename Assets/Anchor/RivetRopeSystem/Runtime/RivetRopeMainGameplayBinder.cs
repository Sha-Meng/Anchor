using System;
using System.Reflection;
using UnityEngine;

namespace Anchor.RivetRopeSystem
{
    public sealed class RivetRopeMainGameplayBinder : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RivetRopeDebugDriver driver;
        [SerializeField] private Transform upperAttachPoint;
        [SerializeField] private Transform lowerAttachPoint;
        [SerializeField] private Transform placeFallbackPoint;
        [SerializeField] private Transform collectProbePoint;
        [SerializeField] private Camera targetCamera;

        [Header("3C Runtime Lookup")]
        [SerializeField] private string climbControllerObjectName = "Climb3C_Controller";
        [SerializeField] private string climbControllerTypeName = "ClimbGame.Climb3C.Gameplay.ClimbController3D";
        [SerializeField] private string remoteClimberNamePrefix = "Remote Climber";
        [SerializeField] private Vector3 waistOffset = new Vector3(0f, -0.35f, 0f);
        [SerializeField] private float ropeDepthOffset = 0.22f;
        [SerializeField] private float ropeSideOffset = 0.32f;
        [SerializeField] private Vector3 lowerOffsetFromUpper = new Vector3(0f, -3.2f, 0f);
        [SerializeField] private float probeFollowSmoothTime = 0.12f;
        [SerializeField] private bool autoStartRescueOnFalling = true;
        [SerializeField] private bool logBinding = true;

        private Component _climbController;
        private PropertyInfo _torsoCenterProperty;
        private PropertyInfo _stateProperty;
        private Transform _remoteClimberRoot;
        private Vector3 _upperVelocity;
        private Vector3 _lowerVelocity;
        private bool _wasFalling;

        public bool IsBound => _climbController != null;
        public Vector3 UpperWaistPosition => upperAttachPoint != null ? upperAttachPoint.position : transform.position;
        public Vector3 LowerWaistPosition => lowerAttachPoint != null ? lowerAttachPoint.position : transform.position;
        public Vector3 PlaceCandidatePosition => placeFallbackPoint != null ? placeFallbackPoint.position : UpperWaistPosition + Vector3.up * 0.6f;
        public Vector3 CollectProbePosition => collectProbePoint != null ? collectProbePoint.position : LowerWaistPosition;
        public bool CanPlaceLeadRivet => driver != null && IsBound && IsStableForInteraction() && driver.Model.GetInventory(driver.Model.LeadPlayerId) > 0;
        public bool CanRescuePull => driver != null && driver.Model.RescueState.IsActive && IsStableForInteraction();

        public static string DebugPlaceFirstUiRivet()
        {
            var binder = FindObjectOfType<RivetRopeMainGameplayBinder>();
            if (binder == null)
            {
                const string missing = "Rivet rope main UI place: no RivetRopeMainGameplayBinder found";
                Debug.LogError(missing);
                return missing;
            }

            var result = binder.PlaceLeadRivetFromUi();
            var summary = $"Rivet rope main UI place: success={result.Success}, reason={result.FailureReason}";
            Debug.Log(summary, binder);
            return summary;
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        private void LateUpdate()
        {
            if (_climbController == null)
            {
                TryBindClimbController();
            }

            if (_climbController == null || _torsoCenterProperty == null)
            {
                return;
            }

            var rawUpper = (Vector3)_torsoCenterProperty.GetValue(_climbController, null) + waistOffset;
            var rawLower = ResolveLowerWaist(rawUpper);
            var upper = ApplyRopePresentationOffset(rawUpper);
            var lower = ApplyRopePresentationOffset(rawLower);
            var smoothTime = Mathf.Max(0.01f, probeFollowSmoothTime);

            if (upperAttachPoint != null)
            {
                upperAttachPoint.position = Vector3.SmoothDamp(upperAttachPoint.position, upper, ref _upperVelocity, smoothTime);
            }

            if (lowerAttachPoint != null)
            {
                lowerAttachPoint.position = Vector3.SmoothDamp(lowerAttachPoint.position, lower, ref _lowerVelocity, smoothTime);
            }

            if (placeFallbackPoint != null)
            {
                placeFallbackPoint.position = ApplyRopePresentationOffset(rawUpper + Vector3.up * 0.9f);
            }

            if (collectProbePoint != null)
            {
                collectProbePoint.position = placeFallbackPoint != null ? placeFallbackPoint.position : upper;
            }

            UpdateFallRescueState();
        }

        public RivetOperationResult PlaceLeadRivetFromUi()
        {
            if (driver == null || !CanPlaceLeadRivet)
            {
                return RivetOperationResult.Failed(RivetRopeFailureReason.PlayerNotInteractive);
            }

            return driver.PlaceRivet(new RivetPlaceRequest
            {
                PlayerId = driver.LeadPlayerId,
                Position = PlaceCandidatePosition,
                IsValidSurface = true,
                IsPlayerInteractive = true
            });
        }

        public bool TryGetNearestCollectableRivet(out PlacedRivet rivet)
        {
            rivet = default;
            if (driver == null || driver.Model.PlacedRivets.Count == 0)
            {
                return false;
            }

            var origin = CollectProbePosition;
            var nearest = driver.Model.PlacedRivets[0];
            var nearestDistance = Vector3.Distance(origin, nearest.Position);
            for (int i = 1; i < driver.Model.PlacedRivets.Count; i++)
            {
                var candidate = driver.Model.PlacedRivets[i];
                var distance = Vector3.Distance(origin, candidate.Position);
                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            var request = new RivetCollectRequest
            {
                PlayerId = driver.SecondPlayerId,
                RivetId = nearest.RivetId,
                PlayerPosition = origin,
                IsPlayerStable = true,
                IsPlayerInteractive = true
            };

            if (!driver.CanCollectRivet(request, out _))
            {
                return false;
            }

            rivet = nearest;
            return true;
        }

        public RivetOperationResult CollectNearestRivetFromUi()
        {
            if (driver == null || !TryGetNearestCollectableRivet(out var rivet))
            {
                return RivetOperationResult.Failed(RivetRopeFailureReason.RivetNotFound);
            }

            return driver.CollectRivet(new RivetCollectRequest
            {
                PlayerId = driver.SecondPlayerId,
                RivetId = rivet.RivetId,
                PlayerPosition = CollectProbePosition,
                IsPlayerStable = true,
                IsPlayerInteractive = true
            });
        }

        public RescuePullResult RescuePullFromUi()
        {
            return driver != null && CanRescuePull
                ? driver.ApplyRescueClick(true, true)
                : new RescuePullResult { Success = false, FailureReason = RivetRopeFailureReason.PlayerNotInteractive };
        }

        private void TryBindClimbController()
        {
            var target = GameObject.Find(climbControllerObjectName);
            if (target == null)
            {
                return;
            }

            var controllerType = ResolveType(climbControllerTypeName);
            _climbController = controllerType != null ? target.GetComponent(controllerType) : null;
            if (_climbController == null)
            {
                return;
            }

            _torsoCenterProperty = _climbController.GetType().GetProperty("TorsoCenter", BindingFlags.Instance | BindingFlags.Public);
            _stateProperty = _climbController.GetType().GetProperty("State", BindingFlags.Instance | BindingFlags.Public);

            if (logBinding)
            {
                Debug.Log("Rivet rope main gameplay bound to Climb3C_Controller", this);
            }
        }

        private Vector3 ResolveLowerWaist(Vector3 upper)
        {
            if (_remoteClimberRoot == null)
            {
                _remoteClimberRoot = FindRemoteClimberRoot();
            }

            if (_remoteClimberRoot != null && _remoteClimberRoot.gameObject.activeInHierarchy)
            {
                return _remoteClimberRoot.position + waistOffset;
            }

            return upper + lowerOffsetFromUpper;
        }

        private Vector3 ApplyRopePresentationOffset(Vector3 position)
        {
            if (targetCamera == null)
            {
                return position;
            }

            var cameraTransform = targetCamera.transform;
            var depthOffset = ropeDepthOffset > 0f ? cameraTransform.forward.normalized * ropeDepthOffset : Vector3.zero;
            var sideOffset = Mathf.Abs(ropeSideOffset) > 0f ? cameraTransform.right.normalized * ropeSideOffset : Vector3.zero;
            return position + depthOffset + sideOffset;
        }

        private bool IsStableForInteraction()
        {
            if (_stateProperty == null || _climbController == null)
            {
                return IsBound;
            }

            var stateValue = _stateProperty.GetValue(_climbController, null);
            var state = Convert.ToString(stateValue);
            return string.Equals(state, "WaitingForPress", StringComparison.Ordinal);
        }

        private Transform FindRemoteClimberRoot()
        {
            if (string.IsNullOrEmpty(remoteClimberNamePrefix))
            {
                return null;
            }

            var all = FindObjectsOfType<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name.StartsWith(remoteClimberNamePrefix, StringComparison.Ordinal))
                {
                    return all[i];
                }
            }

            return null;
        }

        private void UpdateFallRescueState()
        {
            if (!autoStartRescueOnFalling || driver == null || _stateProperty == null)
            {
                return;
            }

            var stateValue = _stateProperty.GetValue(_climbController, null);
            var isFalling = string.Equals(Convert.ToString(stateValue), "Falling", StringComparison.Ordinal);
            if (isFalling && !_wasFalling)
            {
                driver.StartRescueWindow();
                driver.DebugResolveLeadFall();
            }

            _wasFalling = isFalling;
        }

        private static Type ResolveType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
