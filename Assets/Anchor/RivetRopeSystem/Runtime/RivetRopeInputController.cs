using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Anchor.RivetRopeSystem
{
    public sealed class RivetRopeInputController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RivetRopeDebugDriver driver;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform placeFallbackPoint;
        [SerializeField] private Transform collectPlayerPoint;
        [SerializeField] private LayerMask placeSurfaceMask = ~0;
        [SerializeField] private float maxRayDistance = 200f;

        [Header("Keyboard")]
        [SerializeField] private KeyCode placeKey = KeyCode.E;
        [SerializeField] private KeyCode collectKey = KeyCode.R;
        [SerializeField] private KeyCode rescueKey = KeyCode.Q;
        [SerializeField] private KeyCode fallKey = KeyCode.F;
        [SerializeField] private KeyCode switchLeadKey = KeyCode.Tab;
        [SerializeField] private bool enableMouseClickPlace = true;
        [SerializeField] private bool ignoreUiPointerInput = true;

        [Header("Touch")]
        [SerializeField] private bool enableTouchZones = true;
        [SerializeField] private float touchPlaceZoneMaxX = 0.33f;
        [SerializeField] private float touchCollectZoneMinX = 0.33f;
        [SerializeField] private float touchCollectZoneMaxX = 0.66f;

        [Header("State")]
        [SerializeField] private bool playerStable = true;
        [SerializeField] private bool playerInteractive = true;
        [SerializeField] private bool placeSurfaceValid = true;
        [SerializeField] private bool logInputActions = true;

        [Header("Audio")]
        [SerializeField] private AudioClip placeRivetClip;
        [SerializeField, Range(0f, 1f)] private float placeRivetVolume = 1f;

        private PointerEventData _uiPointerData;
        private readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>();
        private AudioSource _audioSource;

        public static string DebugRunFirstInputSmokeSequence()
        {
            var controller = FindObjectOfType<RivetRopeInputController>();
            if (controller == null)
            {
                const string missing = "Rivet rope input smoke: no RivetRopeInputController found";
                Debug.LogError(missing);
                return missing;
            }

            return controller.DebugRunInputSmokeSequence();
        }

        public string DebugRunInputSmokeSequence()
        {
            if (driver == null)
            {
                const string missing = "Rivet rope input smoke: no driver";
                Debug.LogError(missing, this);
                return missing;
            }

            driver.ResetModel();
            var originalCamera = targetCamera;
            targetCamera = null;
            TriggerPlace();
            targetCamera = originalCamera;
            TriggerFallWindow();
            var protectedDamage = driver.LastFall.SuggestedDamage;
            TriggerRescuePull();
            var rescueDamage = driver.LastFall.SuggestedDamage;
            TriggerCollectNearest();
            var afterCollect = driver.DebugResolveLeadFall();
            TriggerSwitchLead();

            var summary =
                "Rivet rope input smoke: " +
                $"placed={driver.Model.PlacedRivets.Count}, " +
                $"revision={driver.Model.RopeRevision}, " +
                $"protectedDamage={protectedDamage:0.0}, " +
                $"rescueDamage={rescueDamage:0.0}, " +
                $"afterCollectProtected={afterCollect.IsProtected}, " +
                $"lead={driver.Model.LeadPlayerId}, " +
                $"second={driver.Model.SecondPlayerId}";

            Debug.Log(summary, this);
            return summary;
        }

        public void TriggerPlace()
        {
            if (driver == null)
            {
                return;
            }

            var position = ResolvePlacePosition();
            var result = driver.PlaceRivet(new RivetPlaceRequest
            {
                PlayerId = driver.LeadPlayerId,
                Position = position,
                IsValidSurface = placeSurfaceValid,
                IsPlayerInteractive = playerInteractive
            });
            if (result.Success)
            {
                PlayPlaceRivetClip();
            }
            LogOperation("place", result.Success, result.FailureReason);
        }

        public void TriggerCollectNearest()
        {
            if (driver == null || driver.Model.PlacedRivets.Count == 0)
            {
                return;
            }

            var playerPosition = collectPlayerPoint != null ? collectPlayerPoint.position : transform.position;
            var nearest = driver.Model.PlacedRivets[0];
            var nearestDistance = Vector3.Distance(playerPosition, nearest.Position);
            for (int i = 1; i < driver.Model.PlacedRivets.Count; i++)
            {
                var candidate = driver.Model.PlacedRivets[i];
                var distance = Vector3.Distance(playerPosition, candidate.Position);
                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            var result = driver.CollectRivet(new RivetCollectRequest
            {
                PlayerId = driver.SecondPlayerId,
                RivetId = nearest.RivetId,
                PlayerPosition = playerPosition,
                IsPlayerStable = playerStable,
                IsPlayerInteractive = playerInteractive
            });
            LogOperation("collect", result.Success, result.FailureReason);
        }

        public void TriggerRescuePull()
        {
            if (driver == null)
            {
                return;
            }

            var result = driver.ApplyRescueClick(playerStable, playerInteractive);
            driver.DebugResolveLeadFall();
            LogOperation("rescue", result.Success, result.FailureReason);
        }

        public void TriggerFallWindow()
        {
            if (driver == null)
            {
                return;
            }

            driver.StartRescueWindow();
            driver.DebugResolveLeadFall();
            LogInput("fall window");
        }

        public void TriggerSwitchLead()
        {
            if (driver == null)
            {
                return;
            }

            driver.TrySwitchLead(playerInteractive, playerInteractive, false, out var failureReason);
            LogInput("switch lead " + failureReason);
        }

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
            ResolvePlaceRivetClip();
        }

        private void Update()
        {
            if (driver == null)
            {
                return;
            }

            UpdateKeyboardAndMouse();
            UpdateTouchZones();
        }

        private void UpdateKeyboardAndMouse()
        {
            if (Input.GetKeyDown(placeKey) ||
                (enableMouseClickPlace && Input.GetMouseButtonDown(0) && !IsPointerOverUi(Input.mousePosition)))
            {
                TriggerPlace();
            }

            if (Input.GetKeyDown(collectKey))
            {
                TriggerCollectNearest();
            }

            if (Input.GetKeyDown(rescueKey))
            {
                TriggerRescuePull();
            }

            if (Input.GetKeyDown(fallKey))
            {
                TriggerFallWindow();
            }

            if (Input.GetKeyDown(switchLeadKey))
            {
                TriggerSwitchLead();
            }
        }

        private void UpdateTouchZones()
        {
            if (!enableTouchZones || Input.touchCount <= 0)
            {
                return;
            }

            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                {
                    continue;
                }

                if (IsPointerOverUi(touch.position, touch.fingerId))
                {
                    continue;
                }

                var normalizedX = touch.position.x / Mathf.Max(1f, Screen.width);
                if (normalizedX < touchPlaceZoneMaxX)
                {
                    TriggerPlace();
                }
                else if (normalizedX >= touchCollectZoneMinX && normalizedX < touchCollectZoneMaxX)
                {
                    TriggerCollectNearest();
                }
                else
                {
                    TriggerRescuePull();
                }
            }
        }

        private bool IsPointerOverUi(Vector2 screenPosition)
        {
            if (!ignoreUiPointerInput || EventSystem.current == null)
            {
                return false;
            }

            return EventSystem.current.IsPointerOverGameObject() ||
                RaycastUiAt(screenPosition, -1);
        }

        private bool IsPointerOverUi(Vector2 screenPosition, int pointerId)
        {
            if (!ignoreUiPointerInput || EventSystem.current == null)
            {
                return false;
            }

            return EventSystem.current.IsPointerOverGameObject(pointerId) ||
                RaycastUiAt(screenPosition, pointerId);
        }

        private bool RaycastUiAt(Vector2 screenPosition, int pointerId)
        {
            if (_uiPointerData == null)
            {
                _uiPointerData = new PointerEventData(EventSystem.current);
            }

            _uiPointerData.Reset();
            _uiPointerData.pointerId = pointerId;
            _uiPointerData.position = screenPosition;
            _uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(_uiPointerData, _uiRaycastResults);
            return _uiRaycastResults.Count > 0;
        }

        private Vector3 ResolvePlacePosition()
        {
            if (targetCamera != null)
            {
                var ray = targetCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, maxRayDistance, placeSurfaceMask, QueryTriggerInteraction.Ignore))
                {
                    return hit.point;
                }
            }

            return placeFallbackPoint != null ? placeFallbackPoint.position : transform.position;
        }

        private void LogOperation(string action, bool success, RivetRopeFailureReason reason)
        {
            LogInput($"{action}: success={success}, reason={reason}");
        }

        private void LogInput(string message)
        {
            if (logInputActions)
            {
                Debug.Log("Rivet rope input " + message, this);
            }
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
            var assetDatabaseType = System.Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            var loadMethod = assetDatabaseType != null
                ? assetDatabaseType.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(System.Type) })
                : null;
            return loadMethod != null
                ? loadMethod.Invoke(null, new object[] { assetPath, typeof(AudioClip) }) as AudioClip
                : null;
        }
#endif
    }
}
