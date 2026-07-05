using System;
using System.Collections.Generic;
using UnityEngine;

namespace Anchor.RivetRopeSystem
{
    public static class RivetRopeEventTypes
    {
        public const string RivetPlace = "rivet.place";
        public const string RivetCollect = "rivet.collect";
        public const string LeadSwitch = "rivet.leadSwitch";
    }

    public enum RivetRopeFailureReason
    {
        None = 0,
        InvalidPlayer = 1,
        InvalidSurface = 2,
        PlayerNotInteractive = 3,
        NoInventory = 4,
        RivetNotFound = 5,
        OutOfRange = 6,
        PlayerNotStable = 7,
        DuplicateEvent = 8,
        StaleEvent = 9
    }

    public enum RivetRopeTensionState
    {
        Slack = 0,
        Taut = 1
    }

    public enum RivetFallProtectionState
    {
        Unprotected = 0,
        Protected = 1
    }

    public enum RopeForceFeedbackReason
    {
        None = 0,
        Disabled = 1,
        InvalidPath = 2,
        Slack = 3,
        PreTension = 4,
        Taut = 5
    }

    [Serializable]
    public struct RivetRopeSettings
    {
        [Min(0)] public int TotalRivets;
        [Min(0f)] public float TotalRopeLength;
        [Min(0f)] public float PlaceRange;
        [Min(0f)] public float CollectRange;
        [Min(0f)] public float RescueWindowSeconds;
        [Min(0f)] public float RescuePullPerClick;
        [Min(0f)] public float MaxRescuePull;
        [Min(0f)] public float DamagePerMeter;
        [Min(0f)] public float MinFallDamage;
        [Min(0f)] public float MaxFallDamage;
        [Min(0f)] public float ProtectedDamageMultiplier;
        [Min(0f)] public float UnprotectedDamageMultiplier;
        public bool EnableForceFeedback;
        [Min(0f)] public float ForcePreTensionThreshold;
        [Min(0f)] public float ForceMaxConstraintCorrection;
        [Min(0f)] public float ForceElasticStretch;
        [Min(0f)] public float ForceTensionStrengthPerMeter;
        [Min(0f)] public float ForceVelocityDamping;
        [Min(0f)] public float ForceReboundStrength;
        [Min(0f)] public float ForceMaxFeedbackStrength;

        public static RivetRopeSettings CreateDefault()
        {
            return new RivetRopeSettings
            {
                TotalRivets = 5,
                TotalRopeLength = 20f,
                PlaceRange = 1.2f,
                CollectRange = 1.5f,
                RescueWindowSeconds = 1.5f,
                RescuePullPerClick = 0.75f,
                MaxRescuePull = 4f,
                DamagePerMeter = 10f,
                MinFallDamage = 1f,
                MaxFallDamage = 100f,
                ProtectedDamageMultiplier = 0.45f,
                UnprotectedDamageMultiplier = 1f,
                EnableForceFeedback = true,
                ForcePreTensionThreshold = 0.75f,
                ForceMaxConstraintCorrection = 0.45f,
                ForceElasticStretch = 0.35f,
                ForceTensionStrengthPerMeter = 1.25f,
                ForceVelocityDamping = 0.85f,
                ForceReboundStrength = 0.45f,
                ForceMaxFeedbackStrength = 8f
            };
        }

        public RivetRopeSettings Sanitized()
        {
            var maxDamage = Mathf.Max(0f, MaxFallDamage);
            var minDamage = Mathf.Clamp(MinFallDamage, 0f, maxDamage > 0f ? maxDamage : float.MaxValue);

            return new RivetRopeSettings
            {
                TotalRivets = Mathf.Max(0, TotalRivets),
                TotalRopeLength = Mathf.Max(0.01f, TotalRopeLength),
                PlaceRange = Mathf.Max(0f, PlaceRange),
                CollectRange = Mathf.Max(0f, CollectRange),
                RescueWindowSeconds = Mathf.Max(0f, RescueWindowSeconds),
                RescuePullPerClick = Mathf.Max(0f, RescuePullPerClick),
                MaxRescuePull = Mathf.Max(0f, MaxRescuePull),
                DamagePerMeter = Mathf.Max(0f, DamagePerMeter),
                MinFallDamage = minDamage,
                MaxFallDamage = maxDamage > 0f ? maxDamage : 100f,
                ProtectedDamageMultiplier = Mathf.Max(0f, ProtectedDamageMultiplier),
                UnprotectedDamageMultiplier = Mathf.Max(0f, UnprotectedDamageMultiplier),
                EnableForceFeedback = EnableForceFeedback,
                ForcePreTensionThreshold = Mathf.Max(0f, ForcePreTensionThreshold),
                ForceMaxConstraintCorrection = Mathf.Max(0f, ForceMaxConstraintCorrection),
                ForceElasticStretch = Mathf.Max(0f, ForceElasticStretch),
                ForceTensionStrengthPerMeter = Mathf.Max(0f, ForceTensionStrengthPerMeter),
                ForceVelocityDamping = Mathf.Max(0f, ForceVelocityDamping),
                ForceReboundStrength = Mathf.Max(0f, ForceReboundStrength),
                ForceMaxFeedbackStrength = Mathf.Max(0f, ForceMaxFeedbackStrength)
            };
        }
    }

    [Serializable]
    public struct PlayerRivetInventory
    {
        public string PlayerId;
        public int CarriedCount;
    }

    [Serializable]
    public struct PlacedRivet
    {
        public string RivetId;
        public string DeployerPlayerId;
        public Vector3 Position;
        public string DebugName;

        public bool IsPlaced => !string.IsNullOrEmpty(RivetId);
    }

    [Serializable]
    public struct RivetPlaceRequest
    {
        public string PlayerId;
        public Vector3 Position;
        public bool IsValidSurface;
        public bool IsPlayerInteractive;
        public string RequestedRivetId;
        public string DebugName;
    }

    [Serializable]
    public struct RivetCollectRequest
    {
        public string PlayerId;
        public string RivetId;
        public Vector3 PlayerPosition;
        public bool IsPlayerStable;
        public bool IsPlayerInteractive;
    }

    [Serializable]
    public struct RivetRopeSyncEvent
    {
        public string EventId;
        public string EventType;
        public string ActorPlayerId;
        public string RivetId;
        public Vector3 Position;
        public int InventoryAfter;
        public int RopeRevision;

        public bool IsValid => !string.IsNullOrEmpty(EventId) && !string.IsNullOrEmpty(EventType);
    }

    [Serializable]
    public struct RivetOperationResult
    {
        public bool Success;
        public RivetRopeFailureReason FailureReason;
        public PlacedRivet Rivet;
        public int InventoryAfter;
        public int RopeRevision;
        public RivetRopeSyncEvent SyncEvent;

        public static RivetOperationResult Failed(RivetRopeFailureReason reason)
        {
            return new RivetOperationResult
            {
                Success = false,
                FailureReason = reason,
                Rivet = default,
                InventoryAfter = 0,
                RopeRevision = 0,
                SyncEvent = default
            };
        }
    }

    [Serializable]
    public struct RopePathResult
    {
        public Vector3[] Points;
        public float UsedLength;
        public float RemainingSlack;
        public float ConstraintDistance;
        public RivetRopeTensionState TensionState;

        public bool IsTaut => TensionState == RivetRopeTensionState.Taut;
    }

    [Serializable]
    public struct RopeForceFeedbackInput
    {
        public string PlayerId;
        public RopePathResult Path;
        public Vector3 EndpointPosition;
        public Vector3 EndpointVelocity;
        public float DeltaTime;
    }

    [Serializable]
    public struct RopeForceFeedbackResult
    {
        public bool IsActive;
        public string PlayerId;
        public Vector3 TensionDirection;
        public float TensionStrength;
        public float ConstraintDistance;
        public Vector3 SuggestedVelocityCorrection;
        public Vector3 AdjacentConstraintPoint;
        public RivetRopeTensionState TensionState;
        public RopeForceFeedbackReason Reason;

        public bool HasVelocityCorrection => SuggestedVelocityCorrection.sqrMagnitude > 0.000001f;
    }

    public static class RopeForceFeedbackCalculator
    {
        public static RopeForceFeedbackResult Evaluate(RopeForceFeedbackInput input, RivetRopeSettings settings)
        {
            var sanitized = settings.Sanitized();
            var path = input.Path;
            var points = path.Points;

            if (!sanitized.EnableForceFeedback)
            {
                return Inactive(input, RopeForceFeedbackReason.Disabled);
            }

            if (points == null || points.Length < 2)
            {
                return Inactive(input, RopeForceFeedbackReason.InvalidPath);
            }

            var endpointIndex = ResolveEndpointIndex(points, input.EndpointPosition);
            var adjacentIndex = endpointIndex == 0 ? 1 : points.Length - 2;
            var adjacent = points[adjacentIndex];
            var direction = adjacent - input.EndpointPosition;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = adjacent - points[endpointIndex];
            }

            direction = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.zero;
            var constraintDistance = Mathf.Max(0f, path.ConstraintDistance);
            var inPreTension = constraintDistance <= 0f
                && sanitized.ForcePreTensionThreshold > 0f
                && path.RemainingSlack <= sanitized.ForcePreTensionThreshold;

            if (constraintDistance <= 0f && !inPreTension)
            {
                return new RopeForceFeedbackResult
                {
                    IsActive = false,
                    PlayerId = input.PlayerId ?? string.Empty,
                    TensionDirection = direction,
                    TensionStrength = 0f,
                    ConstraintDistance = 0f,
                    SuggestedVelocityCorrection = Vector3.zero,
                    AdjacentConstraintPoint = adjacent,
                    TensionState = path.TensionState,
                    Reason = RopeForceFeedbackReason.Slack
                };
            }

            var elasticScale = CalculateElasticScale(constraintDistance, sanitized.ForceElasticStretch);
            var softenedConstraintDistance = constraintDistance * elasticScale;
            var overshootStrength = softenedConstraintDistance * sanitized.ForceTensionStrengthPerMeter;
            var preTensionStrength = inPreTension
                ? Mathf.InverseLerp(sanitized.ForcePreTensionThreshold, 0f, path.RemainingSlack) * sanitized.ForceTensionStrengthPerMeter
                : 0f;
            var tensionStrength = Mathf.Clamp(overshootStrength + preTensionStrength, 0f, sanitized.ForceMaxFeedbackStrength);

            var movingAwaySpeed = Mathf.Max(0f, Vector3.Dot(input.EndpointVelocity, -direction));
            var dampingCorrection = direction * movingAwaySpeed * sanitized.ForceVelocityDamping * elasticScale;
            var correctionDistance = sanitized.ForceMaxConstraintCorrection > 0f
                ? Mathf.Min(softenedConstraintDistance, sanitized.ForceMaxConstraintCorrection)
                : softenedConstraintDistance;
            var dt = Mathf.Max(0.0001f, input.DeltaTime);
            var reboundCorrection = direction * (correctionDistance / dt) * sanitized.ForceReboundStrength;

            return new RopeForceFeedbackResult
            {
                IsActive = tensionStrength > 0f || constraintDistance > 0f,
                PlayerId = input.PlayerId ?? string.Empty,
                TensionDirection = direction,
                TensionStrength = tensionStrength,
                ConstraintDistance = constraintDistance,
                SuggestedVelocityCorrection = dampingCorrection + reboundCorrection,
                AdjacentConstraintPoint = adjacent,
                TensionState = path.TensionState,
                Reason = constraintDistance > 0f ? RopeForceFeedbackReason.Taut : RopeForceFeedbackReason.PreTension
            };
        }

        private static float CalculateElasticScale(float constraintDistance, float elasticStretch)
        {
            if (constraintDistance <= 0f)
            {
                return 1f;
            }

            if (elasticStretch <= 0f)
            {
                return 1f;
            }

            var normalizedStretch = Mathf.Clamp01(constraintDistance / elasticStretch);
            return Mathf.Lerp(0.2f, 1f, normalizedStretch * normalizedStretch);
        }

        private static RopeForceFeedbackResult Inactive(RopeForceFeedbackInput input, RopeForceFeedbackReason reason)
        {
            return new RopeForceFeedbackResult
            {
                IsActive = false,
                PlayerId = input.PlayerId ?? string.Empty,
                TensionDirection = Vector3.zero,
                TensionStrength = 0f,
                ConstraintDistance = 0f,
                SuggestedVelocityCorrection = Vector3.zero,
                AdjacentConstraintPoint = Vector3.zero,
                TensionState = input.Path.TensionState,
                Reason = reason
            };
        }

        private static int ResolveEndpointIndex(Vector3[] points, Vector3 endpointPosition)
        {
            var firstDistance = (endpointPosition - points[0]).sqrMagnitude;
            var lastDistance = (endpointPosition - points[points.Length - 1]).sqrMagnitude;
            return firstDistance <= lastDistance ? 0 : points.Length - 1;
        }
    }

    [Serializable]
    public struct RescuePullState
    {
        public bool IsActive;
        public float RemainingWindowSeconds;
        public float PullAmount;
        public int EffectiveClickCount;
    }

    [Serializable]
    public struct RescuePullResult
    {
        public bool Success;
        public RivetRopeFailureReason FailureReason;
        public float AddedPull;
        public RescuePullState StateAfter;
    }

    [Serializable]
    public struct RopeFallResolution
    {
        public string FallingPlayerId;
        public RivetFallProtectionState ProtectionState;
        public string ProtectionRivetId;
        public bool RopeTaut;
        public float EstimatedFreeFallDistance;
        public float RescuePullAmount;
        public float DamageMultiplier;
        public float SuggestedDamage;
        public string Reason;

        public bool IsProtected => ProtectionState == RivetFallProtectionState.Protected;
    }

    public interface IRivetRopeDamageSink
    {
        void OnRivetRopeFallResolved(RopeFallResolution resolution);
    }

    public interface IRivetRopeNetworkSink
    {
        void SendRivetRopeEvent(RivetRopeSyncEvent syncEvent);
    }

    public sealed class RivetRopeModel
    {
        private readonly Dictionary<string, int> _inventoryByPlayer = new Dictionary<string, int>();
        private readonly List<PlacedRivet> _placedRivets = new List<PlacedRivet>();
        private readonly HashSet<string> _handledEventIds = new HashSet<string>();

        private RivetRopeSettings _settings = RivetRopeSettings.CreateDefault();
        private string _leadPlayerId;
        private string _secondPlayerId;
        private int _nextRivetNumber = 1;
        private int _nextEventNumber = 1;

        public int RopeRevision { get; private set; }
        public RescuePullState RescueState { get; private set; }
        public RivetRopeSettings Settings => _settings;
        public string LeadPlayerId => _leadPlayerId;
        public string SecondPlayerId => _secondPlayerId;
        public IReadOnlyList<PlacedRivet> PlacedRivets => _placedRivets;

        public void Reset(RivetRopeSettings settings, string leadPlayerId, string secondPlayerId, int leadStartingRivets = -1, int secondStartingRivets = 0)
        {
            _settings = settings.Sanitized();
            _inventoryByPlayer.Clear();
            _placedRivets.Clear();
            _handledEventIds.Clear();
            _leadPlayerId = leadPlayerId ?? string.Empty;
            _secondPlayerId = secondPlayerId ?? string.Empty;
            _nextRivetNumber = 1;
            _nextEventNumber = 1;
            RopeRevision = 0;
            RescueState = default;

            if (string.IsNullOrEmpty(leadPlayerId))
            {
                return;
            }

            if (leadStartingRivets < 0)
            {
                leadStartingRivets = _settings.TotalRivets;
            }

            leadStartingRivets = Mathf.Clamp(leadStartingRivets, 0, _settings.TotalRivets);
            secondStartingRivets = Mathf.Clamp(secondStartingRivets, 0, _settings.TotalRivets - leadStartingRivets);

            _inventoryByPlayer[leadPlayerId] = leadStartingRivets;

            if (!string.IsNullOrEmpty(secondPlayerId) && secondPlayerId != leadPlayerId)
            {
                _inventoryByPlayer[secondPlayerId] = secondStartingRivets;
            }
        }

        public bool TrySwitchLead(bool leadInteractive, bool secondInteractive, bool isFallResolving, out RivetRopeFailureReason failureReason)
        {
            failureReason = RivetRopeFailureReason.None;

            if (!HasPlayer(_leadPlayerId) || !HasPlayer(_secondPlayerId))
            {
                failureReason = RivetRopeFailureReason.InvalidPlayer;
                return false;
            }

            if (isFallResolving || !leadInteractive || !secondInteractive)
            {
                failureReason = RivetRopeFailureReason.PlayerNotInteractive;
                return false;
            }

            var previousLead = _leadPlayerId;
            _leadPlayerId = _secondPlayerId;
            _secondPlayerId = previousLead;
            return true;
        }

        public bool TrySwitchLead(
            bool leadInteractive,
            bool secondInteractive,
            bool isFallResolving,
            out RivetRopeFailureReason failureReason,
            out RivetRopeSyncEvent syncEvent)
        {
            var actorPlayerId = _leadPlayerId;
            syncEvent = default;

            if (!TrySwitchLead(leadInteractive, secondInteractive, isFallResolving, out failureReason))
            {
                return false;
            }

            RopeRevision++;
            syncEvent = CreateSyncEvent(
                RivetRopeEventTypes.LeadSwitch,
                actorPlayerId,
                string.Empty,
                Vector3.zero,
                GetInventory(actorPlayerId));
            return true;
        }

        public bool HasPlayer(string playerId)
        {
            return !string.IsNullOrEmpty(playerId) && _inventoryByPlayer.ContainsKey(playerId);
        }

        public int GetInventory(string playerId)
        {
            return HasPlayer(playerId) ? _inventoryByPlayer[playerId] : 0;
        }

        public int TotalInventoryAndPlacedCount()
        {
            var total = _placedRivets.Count;
            foreach (var kvp in _inventoryByPlayer)
            {
                total += kvp.Value;
            }

            return total;
        }

        public bool TryGetPlacedRivet(string rivetId, out PlacedRivet rivet)
        {
            var index = IndexOfRivet(rivetId);
            if (index < 0)
            {
                rivet = default;
                return false;
            }

            rivet = _placedRivets[index];
            return true;
        }

        public RivetOperationResult TryPlaceRivet(RivetPlaceRequest request)
        {
            if (!HasPlayer(request.PlayerId))
            {
                return RivetOperationResult.Failed(RivetRopeFailureReason.InvalidPlayer);
            }

            if (!request.IsPlayerInteractive)
            {
                return RivetOperationResult.Failed(RivetRopeFailureReason.PlayerNotInteractive);
            }

            if (!request.IsValidSurface)
            {
                return RivetOperationResult.Failed(RivetRopeFailureReason.InvalidSurface);
            }

            var inventory = _inventoryByPlayer[request.PlayerId];
            if (inventory <= 0)
            {
                return RivetOperationResult.Failed(RivetRopeFailureReason.NoInventory);
            }

            var rivetId = string.IsNullOrEmpty(request.RequestedRivetId)
                ? $"rivet-{_nextRivetNumber:000}"
                : request.RequestedRivetId;
            _nextRivetNumber++;

            var rivet = new PlacedRivet
            {
                RivetId = rivetId,
                DeployerPlayerId = request.PlayerId,
                Position = request.Position,
                DebugName = string.IsNullOrEmpty(request.DebugName) ? rivetId : request.DebugName
            };

            _inventoryByPlayer[request.PlayerId] = inventory - 1;
            _placedRivets.Add(rivet);
            RopeRevision++;

            return new RivetOperationResult
            {
                Success = true,
                FailureReason = RivetRopeFailureReason.None,
                Rivet = rivet,
                InventoryAfter = _inventoryByPlayer[request.PlayerId],
                RopeRevision = RopeRevision,
                SyncEvent = CreateSyncEvent(RivetRopeEventTypes.RivetPlace, request.PlayerId, rivet.RivetId, rivet.Position, _inventoryByPlayer[request.PlayerId])
            };
        }

        public bool CanCollectRivet(RivetCollectRequest request, out RivetRopeFailureReason failureReason)
        {
            failureReason = RivetRopeFailureReason.None;

            if (!HasPlayer(request.PlayerId))
            {
                failureReason = RivetRopeFailureReason.InvalidPlayer;
                return false;
            }

            if (!request.IsPlayerInteractive)
            {
                failureReason = RivetRopeFailureReason.PlayerNotInteractive;
                return false;
            }

            if (!request.IsPlayerStable)
            {
                failureReason = RivetRopeFailureReason.PlayerNotStable;
                return false;
            }

            if (!TryGetPlacedRivet(request.RivetId, out var rivet))
            {
                failureReason = RivetRopeFailureReason.RivetNotFound;
                return false;
            }

            if (Vector3.Distance(request.PlayerPosition, rivet.Position) > _settings.CollectRange)
            {
                failureReason = RivetRopeFailureReason.OutOfRange;
                return false;
            }

            return true;
        }

        public RivetOperationResult TryCollectRivet(RivetCollectRequest request)
        {
            if (!CanCollectRivet(request, out var failureReason))
            {
                return RivetOperationResult.Failed(failureReason);
            }

            var index = IndexOfRivet(request.RivetId);
            var rivet = _placedRivets[index];
            _placedRivets.RemoveAt(index);
            _inventoryByPlayer[request.PlayerId] = GetInventory(request.PlayerId) + 1;
            RopeRevision++;

            return new RivetOperationResult
            {
                Success = true,
                FailureReason = RivetRopeFailureReason.None,
                Rivet = rivet,
                InventoryAfter = _inventoryByPlayer[request.PlayerId],
                RopeRevision = RopeRevision,
                SyncEvent = CreateSyncEvent(RivetRopeEventTypes.RivetCollect, request.PlayerId, rivet.RivetId, rivet.Position, _inventoryByPlayer[request.PlayerId])
            };
        }

        public RopePathResult BuildRopePath(Vector3 lowerAttachPoint, Vector3 upperAttachPoint)
        {
            var points = new List<Vector3>(_placedRivets.Count + 2) { lowerAttachPoint };
            var sorted = new List<PlacedRivet>(_placedRivets);
            sorted.Sort((a, b) => a.Position.y.CompareTo(b.Position.y));

            for (int i = 0; i < sorted.Count; i++)
            {
                points.Add(sorted[i].Position);
            }

            points.Add(upperAttachPoint);

            var usedLength = CalculatePathLength(points);
            var slack = _settings.TotalRopeLength - usedLength;

            return new RopePathResult
            {
                Points = points.ToArray(),
                UsedLength = usedLength,
                RemainingSlack = Mathf.Max(0f, slack),
                ConstraintDistance = Mathf.Max(0f, -slack),
                TensionState = slack > 0f ? RivetRopeTensionState.Slack : RivetRopeTensionState.Taut
            };
        }

        public RopeForceFeedbackResult EvaluateForceFeedback(
            string playerId,
            RopePathResult path,
            Vector3 endpointPosition,
            Vector3 endpointVelocity,
            float deltaTime)
        {
            return RopeForceFeedbackCalculator.Evaluate(
                new RopeForceFeedbackInput
                {
                    PlayerId = playerId,
                    Path = path,
                    EndpointPosition = endpointPosition,
                    EndpointVelocity = endpointVelocity,
                    DeltaTime = deltaTime
                },
                _settings);
        }

        public void StartRescueWindow()
        {
            RescueState = new RescuePullState
            {
                IsActive = _settings.RescueWindowSeconds > 0f,
                RemainingWindowSeconds = _settings.RescueWindowSeconds,
                PullAmount = 0f,
                EffectiveClickCount = 0
            };
        }

        public void TickRescueWindow(float deltaTime)
        {
            if (!RescueState.IsActive)
            {
                return;
            }

            var state = RescueState;
            state.RemainingWindowSeconds = Mathf.Max(0f, state.RemainingWindowSeconds - Mathf.Max(0f, deltaTime));
            if (state.RemainingWindowSeconds <= 0f)
            {
                state.IsActive = false;
            }

            RescueState = state;
        }

        public RescuePullResult TryApplyRescueClick(bool rescuerStable, bool rescuerInteractive)
        {
            if (!RescueState.IsActive)
            {
                return new RescuePullResult
                {
                    Success = false,
                    FailureReason = RivetRopeFailureReason.PlayerNotInteractive,
                    StateAfter = RescueState
                };
            }

            if (!rescuerInteractive)
            {
                return new RescuePullResult
                {
                    Success = false,
                    FailureReason = RivetRopeFailureReason.PlayerNotInteractive,
                    StateAfter = RescueState
                };
            }

            if (!rescuerStable)
            {
                return new RescuePullResult
                {
                    Success = false,
                    FailureReason = RivetRopeFailureReason.PlayerNotStable,
                    StateAfter = RescueState
                };
            }

            var state = RescueState;
            var before = state.PullAmount;
            state.PullAmount = Mathf.Min(_settings.MaxRescuePull, state.PullAmount + _settings.RescuePullPerClick);
            var added = state.PullAmount - before;
            if (added > 0f)
            {
                state.EffectiveClickCount++;
            }

            RescueState = state;

            return new RescuePullResult
            {
                Success = added > 0f,
                FailureReason = added > 0f ? RivetRopeFailureReason.None : RivetRopeFailureReason.PlayerNotInteractive,
                AddedPull = added,
                StateAfter = state
            };
        }

        public RopeFallResolution ResolveFall(string fallingPlayerId, Vector3 lowerAttachPoint, Vector3 fallingAttachPoint, float rescuePullAmount = -1f)
        {
            var protectionIndex = FindHighestProtectionRivetIndex(fallingAttachPoint);
            var rescuePull = rescuePullAmount >= 0f ? rescuePullAmount : RescueState.PullAmount;

            if (protectionIndex < 0)
            {
                var unprotectedDistance = Mathf.Abs(fallingAttachPoint.y - lowerAttachPoint.y);
                return BuildFallResolution(
                    fallingPlayerId,
                    RivetFallProtectionState.Unprotected,
                    string.Empty,
                    false,
                    unprotectedDistance,
                    rescuePull,
                    "NoProtectionRivet");
            }

            var protection = _placedRivets[protectionIndex];
            var lowerToProtection = CalculatePathLengthToProtection(lowerAttachPoint, protection.RivetId);
            var protectionToFalling = Vector3.Distance(protection.Position, fallingAttachPoint);
            var availableLength = Mathf.Max(0f, _settings.TotalRopeLength - lowerToProtection - rescuePull);
            var estimatedFreeFall = Mathf.Max(0f, availableLength - protectionToFalling);
            var isTaut = availableLength <= protectionToFalling;

            return BuildFallResolution(
                fallingPlayerId,
                RivetFallProtectionState.Protected,
                protection.RivetId,
                isTaut,
                estimatedFreeFall,
                rescuePull,
                "ProtectedByRivet");
        }

        public RivetOperationResult ApplyRemoteEvent(RivetRopeSyncEvent syncEvent)
        {
            if (!syncEvent.IsValid || string.IsNullOrEmpty(syncEvent.ActorPlayerId))
            {
                return RivetOperationResult.Failed(RivetRopeFailureReason.InvalidPlayer);
            }

            if (!_handledEventIds.Add(syncEvent.EventId))
            {
                return RivetOperationResult.Failed(RivetRopeFailureReason.DuplicateEvent);
            }

            if (syncEvent.RopeRevision <= RopeRevision)
            {
                return RivetOperationResult.Failed(RivetRopeFailureReason.StaleEvent);
            }

            if (!HasPlayer(syncEvent.ActorPlayerId))
            {
                _inventoryByPlayer[syncEvent.ActorPlayerId] = Mathf.Max(0, syncEvent.InventoryAfter);
            }

            if (syncEvent.EventType == RivetRopeEventTypes.RivetPlace)
            {
                var rivet = new PlacedRivet
                {
                    RivetId = syncEvent.RivetId,
                    DeployerPlayerId = syncEvent.ActorPlayerId,
                    Position = syncEvent.Position,
                    DebugName = syncEvent.RivetId
                };

                if (IndexOfRivet(syncEvent.RivetId) < 0)
                {
                    _placedRivets.Add(rivet);
                }

                _inventoryByPlayer[syncEvent.ActorPlayerId] = Mathf.Max(0, syncEvent.InventoryAfter);
                RopeRevision = syncEvent.RopeRevision;

                return new RivetOperationResult
                {
                    Success = true,
                    FailureReason = RivetRopeFailureReason.None,
                    Rivet = rivet,
                    InventoryAfter = _inventoryByPlayer[syncEvent.ActorPlayerId],
                    RopeRevision = RopeRevision,
                    SyncEvent = syncEvent
                };
            }

            if (syncEvent.EventType == RivetRopeEventTypes.RivetCollect)
            {
                var index = IndexOfRivet(syncEvent.RivetId);
                var rivet = index >= 0 ? _placedRivets[index] : default;
                if (index >= 0)
                {
                    _placedRivets.RemoveAt(index);
                }

                _inventoryByPlayer[syncEvent.ActorPlayerId] = Mathf.Max(0, syncEvent.InventoryAfter);
                RopeRevision = syncEvent.RopeRevision;

                return new RivetOperationResult
                {
                    Success = true,
                    FailureReason = RivetRopeFailureReason.None,
                    Rivet = rivet,
                    InventoryAfter = _inventoryByPlayer[syncEvent.ActorPlayerId],
                    RopeRevision = RopeRevision,
                    SyncEvent = syncEvent
                };
            }

            if (syncEvent.EventType == RivetRopeEventTypes.LeadSwitch)
            {
                if (!TrySwitchLead(true, true, false, out var failureReason))
                {
                    return RivetOperationResult.Failed(failureReason);
                }

                RopeRevision = syncEvent.RopeRevision;

                return new RivetOperationResult
                {
                    Success = true,
                    FailureReason = RivetRopeFailureReason.None,
                    Rivet = default,
                    InventoryAfter = GetInventory(syncEvent.ActorPlayerId),
                    RopeRevision = RopeRevision,
                    SyncEvent = syncEvent
                };
            }

            return RivetOperationResult.Failed(RivetRopeFailureReason.InvalidSurface);
        }

        private RopeFallResolution BuildFallResolution(
            string fallingPlayerId,
            RivetFallProtectionState protectionState,
            string protectionRivetId,
            bool ropeTaut,
            float freeFallDistance,
            float rescuePull,
            string reason)
        {
            var multiplier = protectionState == RivetFallProtectionState.Protected
                ? _settings.ProtectedDamageMultiplier
                : _settings.UnprotectedDamageMultiplier;
            var damage = freeFallDistance * _settings.DamagePerMeter * multiplier;
            damage = Mathf.Clamp(damage, _settings.MinFallDamage, _settings.MaxFallDamage);

            return new RopeFallResolution
            {
                FallingPlayerId = fallingPlayerId,
                ProtectionState = protectionState,
                ProtectionRivetId = protectionRivetId,
                RopeTaut = ropeTaut,
                EstimatedFreeFallDistance = freeFallDistance,
                RescuePullAmount = rescuePull,
                DamageMultiplier = multiplier,
                SuggestedDamage = damage,
                Reason = reason
            };
        }

        private int FindHighestProtectionRivetIndex(Vector3 fallingAttachPoint)
        {
            var bestIndex = -1;
            var bestY = float.NegativeInfinity;

            for (int i = 0; i < _placedRivets.Count; i++)
            {
                var rivet = _placedRivets[i];
                if (rivet.Position.y > fallingAttachPoint.y)
                {
                    continue;
                }

                if (rivet.Position.y > bestY)
                {
                    bestY = rivet.Position.y;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private float CalculatePathLengthToProtection(Vector3 lowerAttachPoint, string protectionRivetId)
        {
            var sorted = new List<PlacedRivet>(_placedRivets);
            sorted.Sort((a, b) => a.Position.y.CompareTo(b.Position.y));

            var current = lowerAttachPoint;
            var length = 0f;

            for (int i = 0; i < sorted.Count; i++)
            {
                var next = sorted[i].Position;
                length += Vector3.Distance(current, next);
                current = next;

                if (sorted[i].RivetId == protectionRivetId)
                {
                    return length;
                }
            }

            return length;
        }

        private RivetRopeSyncEvent CreateSyncEvent(string eventType, string actorPlayerId, string rivetId, Vector3 position, int inventoryAfter)
        {
            return new RivetRopeSyncEvent
            {
                EventId = $"rivet-event-{_nextEventNumber++:000}",
                EventType = eventType,
                ActorPlayerId = actorPlayerId,
                RivetId = rivetId,
                Position = position,
                InventoryAfter = inventoryAfter,
                RopeRevision = RopeRevision
            };
        }

        private int IndexOfRivet(string rivetId)
        {
            if (string.IsNullOrEmpty(rivetId))
            {
                return -1;
            }

            for (int i = 0; i < _placedRivets.Count; i++)
            {
                if (_placedRivets[i].RivetId == rivetId)
                {
                    return i;
                }
            }

            return -1;
        }

        private static float CalculatePathLength(IList<Vector3> points)
        {
            var length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                length += Vector3.Distance(points[i - 1], points[i]);
            }

            return length;
        }
    }

}
