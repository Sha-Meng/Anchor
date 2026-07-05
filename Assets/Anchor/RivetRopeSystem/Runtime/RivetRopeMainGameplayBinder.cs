using System;
using System.Reflection;
using UnityEngine;

namespace Anchor.RivetRopeSystem
{
    public enum RivetRopeLocalPlayerRole
    {
        Lead = 0,
        Second = 1
    }

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
        [SerializeField] private RivetRopeLocalPlayerRole localPlayerRole = RivetRopeLocalPlayerRole.Lead;
        [SerializeField] private Vector3 waistOffset = new Vector3(0f, -0.35f, 0f);
        [SerializeField] private float ropeDepthOffset = 0.22f;
        [SerializeField] private float ropeSideOffset = 0.32f;
        [SerializeField] private Vector3 lowerOffsetFromUpper = new Vector3(0f, -3.2f, 0f);
        [SerializeField] private float probeFollowSmoothTime = 0.12f;
        [SerializeField] private bool autoStartRescueOnFalling = true;
        [SerializeField] private bool logBinding = true;

        [Header("Audio")]
        [SerializeField] private AudioClip placeRivetClip;
        [SerializeField, Range(0f, 1f)] private float placeRivetVolume = 1f;

        [Header("Bone Attach")]
        [SerializeField] private bool preferBoneAttachPoints = true;
        [SerializeField] private bool strictBoneFollow = true;
        [SerializeField] private bool applyPresentationOffsetToAttachPoints;
        [SerializeField] private HumanBodyBones localAttachBone = HumanBodyBones.Hips;
        [SerializeField] private HumanBodyBones remoteAttachBone = HumanBodyBones.Hips;
        [SerializeField] private string attachBoneNameFallbacks = "Spine2,Spine1,Spine,Hips,Torso";
        [SerializeField] private Vector3 boneLocalOffset = Vector3.zero;
        [SerializeField] private bool renderRopeBehindCharacters = true;

        private Component _climbController;
        private PropertyInfo _torsoCenterProperty;
        private PropertyInfo _stateProperty;
        private FieldInfo _avatarField;
        private object _climberAvatar;
        private Transform _localAttachBone;
        private Transform _remoteAttachBone;
        private Transform _remoteClimberRoot;
        private Vector3 _upperVelocity;
        private Vector3 _lowerVelocity;
        private bool _wasFalling;
        private Vector3 _fallbackLowerAnchor;
        private bool _hasFallbackLowerAnchor;
        private AudioSource _audioSource;

        public bool IsBound => _climbController != null;
        public Vector3 UpperWaistPosition => upperAttachPoint != null ? upperAttachPoint.position : transform.position;
        public Vector3 LowerWaistPosition => lowerAttachPoint != null ? lowerAttachPoint.position : transform.position;
        public Vector3 PlaceCandidatePosition => placeFallbackPoint != null ? placeFallbackPoint.position : UpperWaistPosition + Vector3.up * 0.6f;
        public Vector3 CollectProbePosition => collectProbePoint != null ? collectProbePoint.position : LowerWaistPosition;
        public string LocalPlayerId => ResolveLocalPlayerId();
        public bool IsLocalLead => driver != null && string.Equals(LocalPlayerId, driver.Model.LeadPlayerId, StringComparison.Ordinal);
        public bool CanPlaceLeadRivet => driver != null && IsLocalLead && IsBound && IsStableForInteraction() && driver.Model.GetInventory(LocalPlayerId) > 0;
        public bool CanRescuePull => driver != null && driver.Model.RescueState.IsActive && IsStableForInteraction();
        public bool CanSwitchLead => driver != null && IsLocalLead && IsBound && IsStableForInteraction();

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
            ResolvePlaceRivetClip();
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

            var rawUpper = ResolveUpperWaist();
            var rawLower = ResolveLowerWaist(rawUpper);
            var upper = applyPresentationOffsetToAttachPoints ? ApplyRopePresentationOffset(rawUpper) : rawUpper;
            var lower = applyPresentationOffsetToAttachPoints ? ApplyRopePresentationOffset(rawLower) : rawLower;
            var smoothTime = Mathf.Max(0.01f, probeFollowSmoothTime);

            if (upperAttachPoint != null)
            {
                upperAttachPoint.position = strictBoneFollow
                    ? upper
                    : Vector3.SmoothDamp(upperAttachPoint.position, upper, ref _upperVelocity, smoothTime);
            }

            if (lowerAttachPoint != null)
            {
                lowerAttachPoint.position = strictBoneFollow
                    ? lower
                    : Vector3.SmoothDamp(lowerAttachPoint.position, lower, ref _lowerVelocity, smoothTime);
            }

            if (placeFallbackPoint != null)
            {
                placeFallbackPoint.position = rawUpper + Vector3.up * 0.9f;
            }

            if (collectProbePoint != null)
            {
                collectProbePoint.position = placeFallbackPoint != null ? placeFallbackPoint.position : rawUpper;
            }

            UpdateFallRescueState();
        }

        public RivetOperationResult PlaceLeadRivetFromUi()
        {
            if (driver == null || !CanPlaceLeadRivet)
            {
                return RivetOperationResult.Failed(RivetRopeFailureReason.PlayerNotInteractive);
            }

            var result = driver.PlaceRivet(new RivetPlaceRequest
            {
                PlayerId = LocalPlayerId,
                Position = PlaceCandidatePosition,
                IsValidSurface = true,
                IsPlayerInteractive = true
            });
            if (result.Success)
            {
                PlayPlaceRivetClip();
            }

            return result;
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
                PlayerId = LocalPlayerId,
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
                PlayerId = LocalPlayerId,
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

        public bool SwitchLeadFromUi(out RivetRopeFailureReason failureReason)
        {
            if (driver == null || !CanSwitchLead)
            {
                failureReason = RivetRopeFailureReason.PlayerNotInteractive;
                return false;
            }

            return driver.TrySwitchLead(true, true, false, out failureReason);
        }

        public void SetLocalPlayerRole(RivetRopeLocalPlayerRole role)
        {
            localPlayerRole = role;
        }

        private string ResolveLocalPlayerId()
        {
            if (driver == null)
            {
                return string.Empty;
            }

            return localPlayerRole == RivetRopeLocalPlayerRole.Second
                ? driver.SecondPlayerId
                : driver.LeadPlayerId;
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
            _avatarField = _climbController.GetType().GetField("_avatar", BindingFlags.Instance | BindingFlags.NonPublic);
            _climberAvatar = _avatarField != null ? _avatarField.GetValue(_climbController) : null;
            _localAttachBone = ResolveAttachBone(_climberAvatar, localAttachBone);

            if (logBinding)
            {
                Debug.Log(
                    $"Rivet rope main gameplay bound to Climb3C_Controller, local bone={(_localAttachBone != null ? _localAttachBone.name : "TorsoCenter fallback")}",
                    this);
            }
        }

        private Vector3 ResolveUpperWaist()
        {
            if (preferBoneAttachPoints)
            {
                if (_localAttachBone == null && _climbController != null)
                {
                    _climberAvatar = _avatarField != null ? _avatarField.GetValue(_climbController) : _climberAvatar;
                    _localAttachBone = ResolveAttachBone(_climberAvatar, localAttachBone);
                }

                if (_localAttachBone != null)
                {
                    return _localAttachBone.TransformPoint(boneLocalOffset);
                }
            }

            return (Vector3)_torsoCenterProperty.GetValue(_climbController, null) + waistOffset;
        }

        private Vector3 ResolveLowerWaist(Vector3 upper)
        {
            if (_remoteClimberRoot == null)
            {
                _remoteClimberRoot = FindRemoteClimberRoot();
                _remoteAttachBone = null;
            }

            if (_remoteClimberRoot != null && _remoteClimberRoot.gameObject.activeInHierarchy)
            {
                if (preferBoneAttachPoints)
                {
                    if (_remoteAttachBone == null)
                    {
                        _remoteAttachBone = ResolveAttachBone(_remoteClimberRoot, remoteAttachBone);
                    }

                    if (_remoteAttachBone != null)
                    {
                        return _remoteAttachBone.TransformPoint(boneLocalOffset);
                    }
                }

                return _remoteClimberRoot.position + waistOffset;
            }

            if (IsFalling())
            {
                if (TryGetFallbackFallAnchor(out var fallAnchor))
                {
                    _fallbackLowerAnchor = fallAnchor;
                    _hasFallbackLowerAnchor = true;
                    return fallAnchor;
                }

                if (_hasFallbackLowerAnchor)
                {
                    return _fallbackLowerAnchor;
                }
            }
            else
            {
                _fallbackLowerAnchor = upper + lowerOffsetFromUpper;
                _hasFallbackLowerAnchor = true;
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
            var depthSign = renderRopeBehindCharacters ? 1f : -1f;
            var depthOffset = ropeDepthOffset > 0f ? cameraTransform.forward.normalized * (ropeDepthOffset * depthSign) : Vector3.zero;
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

        private bool IsFalling()
        {
            if (_stateProperty == null || _climbController == null)
            {
                return false;
            }

            var stateValue = _stateProperty.GetValue(_climbController, null);
            return string.Equals(Convert.ToString(stateValue), "Falling", StringComparison.Ordinal);
        }

        private bool TryGetFallbackFallAnchor(out Vector3 anchor)
        {
            anchor = default;
            if (driver == null || driver.Model.PlacedRivets.Count == 0)
            {
                return false;
            }

            var upper = upperAttachPoint != null ? upperAttachPoint.position : transform.position;
            var best = driver.Model.PlacedRivets[0].Position;
            var bestSqr = (best - upper).sqrMagnitude;
            for (int i = 1; i < driver.Model.PlacedRivets.Count; i++)
            {
                var candidate = driver.Model.PlacedRivets[i].Position;
                var sqr = (candidate - upper).sqrMagnitude;
                if (sqr >= bestSqr)
                {
                    continue;
                }

                best = candidate;
                bestSqr = sqr;
            }

            anchor = best;
            return true;
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

        private Transform ResolveAttachBone(object avatarOrRoot, HumanBodyBones humanoidBone)
        {
            var root = ResolveAvatarRoot(avatarOrRoot);
            if (root == null)
            {
                return null;
            }

            var animator = root.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                var bone = animator.GetBoneTransform(humanoidBone);
                if (bone != null)
                {
                    return bone;
                }
            }

            return FindNamedAttachBone(root);
        }

        private static Transform ResolveAvatarRoot(object avatarOrRoot)
        {
            if (avatarOrRoot == null)
            {
                return null;
            }

            if (avatarOrRoot is Transform transform)
            {
                return transform;
            }

            var rootProperty = avatarOrRoot.GetType().GetProperty("Root", BindingFlags.Instance | BindingFlags.Public);
            return rootProperty != null ? rootProperty.GetValue(avatarOrRoot, null) as Transform : null;
        }

        private Transform FindNamedAttachBone(Transform root)
        {
            if (root == null || string.IsNullOrEmpty(attachBoneNameFallbacks))
            {
                return null;
            }

            var names = attachBoneNameFallbacks.Split(',');
            for (int i = 0; i < names.Length; i++)
            {
                var name = names[i].Trim();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var found = FindDeep(root, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (string.Equals(root.name, name, StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var result = FindDeep(root.GetChild(i), name);
                if (result != null)
                {
                    return result;
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

        private void PlayPlaceRivetClip()
        {
            AudioClip clip = ResolvePlaceRivetClip();
            if (clip == null)
            {
                return;
            }

            AudioSource source = EnsureAudioSource();
            source.PlayOneShot(clip, placeRivetVolume);
        }

        private AudioSource EnsureAudioSource()
        {
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 0f;
                _audioSource.playOnAwake = false;
            }

            return _audioSource;
        }

        private AudioClip ResolvePlaceRivetClip()
        {
#if UNITY_EDITOR
            if (placeRivetClip == null)
            {
                placeRivetClip = LoadEditorAudioClipAtPath("Assets/Art/Audio/打锚钉.mp3");
            }
#endif
            return placeRivetClip;
        }

#if UNITY_EDITOR
        private static AudioClip LoadEditorAudioClipAtPath(string assetPath)
        {
            var assetDatabaseType = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            var loadMethod = assetDatabaseType != null
                ? assetDatabaseType.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) })
                : null;
            return loadMethod != null
                ? loadMethod.Invoke(null, new object[] { assetPath, typeof(AudioClip) }) as AudioClip
                : null;
        }
#endif
    }
}
