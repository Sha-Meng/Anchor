using Anchor.ForceSystem;
using UnityEngine;

namespace Anchor.RivetRopeSystem
{
    public sealed class RivetRopeDebugDriver : MonoBehaviour, IForceFallEventSink
    {
        [Header("配置")]
        [SerializeField] private RivetRopeConfig config;
        [SerializeField] private string leadPlayerId = "lead";
        [SerializeField] private string secondPlayerId = "second";
        [SerializeField] private Transform lowerAttachPoint;
        [SerializeField] private Transform upperAttachPoint;
        [SerializeField] private MonoBehaviour damageSinkSource;
        [SerializeField] private MonoBehaviour networkSinkSource;

        [Header("调试")]
        [SerializeField] private bool logOperations = true;
        [SerializeField] private bool drawGizmos = true;

        private readonly RivetRopeModel _model = new RivetRopeModel();
        private RopePathResult _lastPath;
        private RopeFallResolution _lastFall;
        private IRivetRopeDamageSink _damageSink;
        private IRivetRopeNetworkSink _networkSink;

        public RivetRopeModel Model => _model;
        public RopePathResult LastPath => _lastPath;
        public RopeFallResolution LastFall => _lastFall;
        public string LeadPlayerId => leadPlayerId;
        public string SecondPlayerId => secondPlayerId;

        public static string DebugRunFirstSmokeSequence()
        {
            var driver = FindObjectOfType<RivetRopeDebugDriver>();
            if (driver == null)
            {
                const string missing = "Rivet rope smoke: no RivetRopeDebugDriver found";
                Debug.LogError(missing);
                return missing;
            }

            return driver.DebugRunSmokeSequence();
        }

        private void Awake()
        {
            ResolveSinks();
            ResetModel();
        }

        private void OnValidate()
        {
            ResolveSinks();
        }

        private void Update()
        {
            ResolveSinks();
            _model.TickRescueWindow(Time.deltaTime);

            if (lowerAttachPoint != null && upperAttachPoint != null)
            {
                _lastPath = _model.BuildRopePath(lowerAttachPoint.position, upperAttachPoint.position);
            }
        }

        public void ResetModel()
        {
            var settings = config != null ? config.Settings : RivetRopeSettings.CreateDefault();
            _model.Reset(settings, leadPlayerId, secondPlayerId);
        }

        public void SetNetworkSinkSource(MonoBehaviour source)
        {
            networkSinkSource = source;
            ResolveSinks();
        }

        public RivetOperationResult DebugPlaceLeadRivet(Vector3 position, bool validSurface = true)
        {
            var result = _model.TryPlaceRivet(new RivetPlaceRequest
            {
                PlayerId = leadPlayerId,
                Position = position,
                IsValidSurface = validSurface,
                IsPlayerInteractive = true
            });

            LogResult("place", result);
            SendNetworkEventIfNeeded(result);
            return result;
        }

        public RivetOperationResult DebugCollectSecondRivet(string rivetId, Vector3 playerPosition, bool stable = true)
        {
            var result = _model.TryCollectRivet(new RivetCollectRequest
            {
                PlayerId = secondPlayerId,
                RivetId = rivetId,
                PlayerPosition = playerPosition,
                IsPlayerStable = stable,
                IsPlayerInteractive = true
            });

            LogResult("collect", result);
            SendNetworkEventIfNeeded(result);
            return result;
        }

        public RivetOperationResult PlaceRivet(RivetPlaceRequest request)
        {
            var result = _model.TryPlaceRivet(request);
            LogResult("place", result);
            SendNetworkEventIfNeeded(result);
            return result;
        }

        public RivetOperationResult CollectRivet(RivetCollectRequest request)
        {
            var result = _model.TryCollectRivet(request);
            LogResult("collect", result);
            SendNetworkEventIfNeeded(result);
            return result;
        }

        public bool CanCollectRivet(RivetCollectRequest request, out RivetRopeFailureReason failureReason)
        {
            return _model.CanCollectRivet(request, out failureReason);
        }

        public RescuePullResult ApplyRescueClick(bool rescuerStable, bool rescuerInteractive)
        {
            var result = _model.TryApplyRescueClick(rescuerStable, rescuerInteractive);
            if (logOperations)
            {
                Debug.Log(
                    $"Rivet rope rescue: success={result.Success}, reason={result.FailureReason}, pull={result.StateAfter.PullAmount:0.00}",
                    this);
            }

            return result;
        }

        public void StartRescueWindow()
        {
            _model.StartRescueWindow();
        }

        public bool TrySwitchLead(bool leadInteractive, bool secondInteractive, bool isFallResolving, out RivetRopeFailureReason failureReason)
        {
            var switched = _model.TrySwitchLead(
                leadInteractive,
                secondInteractive,
                isFallResolving,
                out failureReason,
                out var syncEvent);
            if (logOperations)
            {
                Debug.Log($"Rivet rope switch lead: success={switched}, reason={failureReason}, lead={_model.LeadPlayerId}", this);
            }

            if (switched && syncEvent.IsValid)
            {
                _networkSink?.SendRivetRopeEvent(syncEvent);
            }

            return switched;
        }

        public RopeFallResolution DebugResolveLeadFall()
        {
            var lower = lowerAttachPoint != null ? lowerAttachPoint.position : Vector3.zero;
            var upper = upperAttachPoint != null ? upperAttachPoint.position : Vector3.up * 8f;
            _lastFall = _model.ResolveFall(leadPlayerId, lower, upper);

            if (logOperations)
            {
                Debug.Log(
                    $"Rivet rope fall: protected={_lastFall.IsProtected}, rivet={_lastFall.ProtectionRivetId}, " +
                    $"distance={_lastFall.EstimatedFreeFallDistance:0.00}, damage={_lastFall.SuggestedDamage:0.0}",
                    this);
            }

            _damageSink?.OnRivetRopeFallResolved(_lastFall);
            return _lastFall;
        }

        public void OnForceFallTriggered(ForceEvaluationResult result)
        {
            StartRescueWindow();
            DebugResolveLeadFall();
        }

        public RivetOperationResult ApplyRemoteEvent(RivetRopeSyncEvent syncEvent)
        {
            var result = _model.ApplyRemoteEvent(syncEvent);
            LogResult("remote " + syncEvent.EventType, result);
            return result;
        }

        public string DebugRunSmokeSequence()
        {
            ResetModel();
            UpdatePath();

            var unprotected = DebugResolveLeadFall();
            var placePosition = GetSampleProtectionPosition();
            var place = DebugPlaceLeadRivet(placePosition);
            UpdatePath();

            var protectedBeforeRescue = DebugResolveLeadFall();
            StartRescueWindow();
            ApplyRescueClick(true, true);
            ApplyRescueClick(true, true);
            ApplyRescueClick(true, true);
            var protectedAfterRescue = DebugResolveLeadFall();

            var collect = place.Success
                ? DebugCollectSecondRivet(place.Rivet.RivetId, place.Rivet.Position, true)
                : RivetOperationResult.Failed(RivetRopeFailureReason.RivetNotFound);
            UpdatePath();

            var afterCollect = DebugResolveLeadFall();
            var switched = _model.TrySwitchLead(true, true, false, out var switchFailure);

            var summary =
                "Rivet rope smoke: " +
                $"place={place.Success}, collect={collect.Success}, " +
                $"unprotectedDamage={unprotected.SuggestedDamage:0.0}, " +
                $"protectedDamage={protectedBeforeRescue.SuggestedDamage:0.0}, " +
                $"rescueDamage={protectedAfterRescue.SuggestedDamage:0.0}, " +
                $"afterCollectProtected={afterCollect.IsProtected}, " +
                $"switchLead={switched}({switchFailure}), " +
                $"placed={_model.PlacedRivets.Count}, revision={_model.RopeRevision}";

            Debug.Log(summary, this);
            return summary;
        }

        private void ResolveSinks()
        {
            _damageSink = damageSinkSource as IRivetRopeDamageSink;
            _networkSink = networkSinkSource as IRivetRopeNetworkSink;
        }

        private void UpdatePath()
        {
            if (lowerAttachPoint != null && upperAttachPoint != null)
            {
                _lastPath = _model.BuildRopePath(lowerAttachPoint.position, upperAttachPoint.position);
            }
        }

        private Vector3 GetSampleProtectionPosition()
        {
            if (lowerAttachPoint == null || upperAttachPoint == null)
            {
                return Vector3.up * 2.4f;
            }

            return Vector3.Lerp(lowerAttachPoint.position, upperAttachPoint.position, 0.65f);
        }

        private void SendNetworkEventIfNeeded(RivetOperationResult result)
        {
            if (result.Success && result.SyncEvent.IsValid)
            {
                _networkSink?.SendRivetRopeEvent(result.SyncEvent);
            }
        }

        private void LogResult(string action, RivetOperationResult result)
        {
            if (!logOperations)
            {
                return;
            }

            Debug.Log(
                $"Rivet rope {action}: success={result.Success}, reason={result.FailureReason}, " +
                $"rivet={result.Rivet.RivetId}, inventory={result.InventoryAfter}, revision={result.RopeRevision}",
                this);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos || lowerAttachPoint == null || upperAttachPoint == null)
            {
                return;
            }

            var settings = config != null ? config.Settings : RivetRopeSettings.CreateDefault();
            var preview = _model.BuildRopePath(lowerAttachPoint.position, upperAttachPoint.position);
            Gizmos.color = preview.IsTaut ? Color.red : Color.green;
            var points = preview.Points;
            for (int i = 1; i < points.Length; i++)
            {
                Gizmos.DrawLine(points[i - 1], points[i]);
            }

            Gizmos.color = Color.yellow;
            for (int i = 0; i < _model.PlacedRivets.Count; i++)
            {
                Gizmos.DrawSphere(_model.PlacedRivets[i].Position, 0.08f);
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(lowerAttachPoint.position, settings.CollectRange);
        }
    }
}
