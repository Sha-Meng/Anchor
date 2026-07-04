using System;
using UnityEngine;

namespace Anchor.ForceSystem
{
    public enum ForcePointType
    {
        None = 0,
        ValidHold = 1,
        Obstacle = 2,
        Fake = 3
    }

    public enum ForceState
    {
        Stable = 0,
        LeftHandReleased = 1,
        RightHandReleased = 2,
        BothHandsReleased = 3,
        Falling = 4
    }

    public enum ForceHandFailureReason
    {
        None = 0,
        NoContact = 1,
        NoGripCandidate = 2,
        Obstacle = 3,
        FakePoint = 4,
        GripQualityTooLow = 5,
        StaminaDepleted = 6,
        BodyUnavailable = 7,
        ExternalFailure = 8
    }

    public enum ForceGripQualityBand
    {
        None = 0,
        Invalid = 1,
        LowQuality = 2,
        Stable = 3
    }

    [Serializable]
    public struct ForceEvaluationSettings
    {
        public float StaminaThreshold;
        public float MinGripQuality;
        public float StableGripQuality;
        public float LowQualityStaminaCostMultiplier;
        public float LowQualityInstabilityMultiplier;
        public float SingleHandGraceSeconds;
        public float BothHandsFallDelaySeconds;
        public bool EnableDebugLog;

        public static ForceEvaluationSettings CreateDefault()
        {
            return new ForceEvaluationSettings
            {
                StaminaThreshold = 0.01f,
                MinGripQuality = 0.25f,
                StableGripQuality = 0.7f,
                LowQualityStaminaCostMultiplier = 1.5f,
                LowQualityInstabilityMultiplier = 1.25f,
                SingleHandGraceSeconds = 0.25f,
                BothHandsFallDelaySeconds = 0.18f,
                EnableDebugLog = false
            };
        }

        public ForceEvaluationSettings Sanitized()
        {
            var minGripQuality = Mathf.Clamp01(MinGripQuality);
            var stableGripQuality = Mathf.Clamp01(Mathf.Max(StableGripQuality, minGripQuality));

            return new ForceEvaluationSettings
            {
                StaminaThreshold = Mathf.Max(0f, StaminaThreshold),
                MinGripQuality = minGripQuality,
                StableGripQuality = stableGripQuality,
                LowQualityStaminaCostMultiplier = Mathf.Max(1f, LowQualityStaminaCostMultiplier),
                LowQualityInstabilityMultiplier = Mathf.Max(1f, LowQualityInstabilityMultiplier),
                SingleHandGraceSeconds = Mathf.Max(0f, SingleHandGraceSeconds),
                BothHandsFallDelaySeconds = Mathf.Max(0f, BothHandsFallDelaySeconds),
                EnableDebugLog = EnableDebugLog
            };
        }
    }

    [CreateAssetMenu(menuName = "Anchor/Force System Config", fileName = "ForceSystemConfig")]
    public sealed class ForceSystemConfig : ScriptableObject
    {
        [SerializeField] private ForceEvaluationSettings settings = ForceEvaluationSettings.CreateDefault();

        public ForceEvaluationSettings Settings => settings.Sanitized();

        private void OnValidate()
        {
            settings = settings.Sanitized();
        }
    }

    [Serializable]
    public struct GripQueryResult
    {
        public bool HasCandidate;
        public ForcePointType PointType;
        [Range(0f, 1f)] public float GripQuality;
        public bool IsFakeRevealed;
        public Vector3 SurfaceNormal;
        public string PointId;
        public string DebugName;

        public static GripQueryResult None(Vector3 surfaceNormal = default)
        {
            return new GripQueryResult
            {
                HasCandidate = false,
                PointType = ForcePointType.None,
                GripQuality = 0f,
                IsFakeRevealed = false,
                SurfaceNormal = surfaceNormal == default ? Vector3.forward : surfaceNormal,
                PointId = string.Empty,
                DebugName = "None"
            };
        }

        public static GripQueryResult Candidate(
            ForcePointType pointType,
            float gripQuality,
            bool isFakeRevealed = false,
            Vector3 surfaceNormal = default,
            string pointId = "",
            string debugName = "")
        {
            return new GripQueryResult
            {
                HasCandidate = pointType != ForcePointType.None,
                PointType = pointType,
                GripQuality = Mathf.Clamp01(gripQuality),
                IsFakeRevealed = isFakeRevealed,
                SurfaceNormal = surfaceNormal == default ? Vector3.forward : surfaceNormal.normalized,
                PointId = pointId ?? string.Empty,
                DebugName = debugName ?? string.Empty
            };
        }
    }

    [Serializable]
    public struct HandForceInput
    {
        public bool IsTouching;
        public bool HasGripCandidate;
        public ForcePointType PointType;
        [Range(0f, 1f)] public float GripQuality;
        public bool IsFakeRevealed;
        public float Stamina;
        public Vector3 WorldPosition;
        public string PointId;
        public ForceHandFailureReason ExternalFailureReason;

        public static HandForceInput FromGrip(
            bool isTouching,
            GripQueryResult grip,
            float stamina,
            Vector3 worldPosition = default,
            ForceHandFailureReason externalFailureReason = ForceHandFailureReason.None)
        {
            return new HandForceInput
            {
                IsTouching = isTouching,
                HasGripCandidate = grip.HasCandidate,
                PointType = grip.PointType,
                GripQuality = Mathf.Clamp01(grip.GripQuality),
                IsFakeRevealed = grip.IsFakeRevealed,
                Stamina = Mathf.Max(0f, stamina),
                WorldPosition = worldPosition,
                PointId = grip.PointId ?? string.Empty,
                ExternalFailureReason = externalFailureReason
            };
        }
    }

    [Serializable]
    public struct BodyForceInput
    {
        public bool IsAlreadyFalling;
        public bool IsStunned;

        public bool AllowsClimbing => !IsAlreadyFalling && !IsStunned;
    }

    [Serializable]
    public struct ForceEvaluationInput
    {
        public HandForceInput LeftHand;
        public HandForceInput RightHand;
        public BodyForceInput Body;
        public ForceState PreviousState;
        public float DeltaTime;
    }

    [Serializable]
    public struct ForceEvaluationMemory
    {
        public float SingleHandInvalidSeconds;
        public float BothHandsInvalidSeconds;
        public ForceState PreviousState;

        public static ForceEvaluationMemory CreateDefault()
        {
            return new ForceEvaluationMemory
            {
                SingleHandInvalidSeconds = 0f,
                BothHandsInvalidSeconds = 0f,
                PreviousState = ForceState.Stable
            };
        }

        public void Reset()
        {
            SingleHandInvalidSeconds = 0f;
            BothHandsInvalidSeconds = 0f;
            PreviousState = ForceState.Stable;
        }
    }

    [Serializable]
    public struct ForceHandEvaluation
    {
        public bool IsEffective;
        public bool IsStableGrip;
        public ForceGripQualityBand GripQualityBand;
        public ForceHandFailureReason FailureReason;
        [Range(0f, 1f)] public float GripQuality;
        public float SuggestedStaminaCostMultiplier;

        public static ForceHandEvaluation Failed(ForceHandFailureReason reason, float gripQuality)
        {
            return new ForceHandEvaluation
            {
                IsEffective = false,
                IsStableGrip = false,
                GripQualityBand = ForceGripQualityBand.Invalid,
                FailureReason = reason,
                GripQuality = Mathf.Clamp01(gripQuality),
                SuggestedStaminaCostMultiplier = 1f
            };
        }

        public static ForceHandEvaluation Effective(
            bool isStableGrip,
            float gripQuality,
            float suggestedStaminaCostMultiplier)
        {
            return new ForceHandEvaluation
            {
                IsEffective = true,
                IsStableGrip = isStableGrip,
                GripQualityBand = isStableGrip ? ForceGripQualityBand.Stable : ForceGripQualityBand.LowQuality,
                FailureReason = ForceHandFailureReason.None,
                GripQuality = Mathf.Clamp01(gripQuality),
                SuggestedStaminaCostMultiplier = Mathf.Max(1f, suggestedStaminaCostMultiplier)
            };
        }
    }

    [Serializable]
    public struct ForceEvaluationResult
    {
        public ForceState State;
        public ForceHandEvaluation LeftHand;
        public ForceHandEvaluation RightHand;
        public ForceHandFailureReason PrimaryFailureReason;
        public bool HasUnstableGrip;
        public bool SingleHandGraceExpired;
        public bool FallTriggered;
        public float SingleHandInvalidSeconds;
        public float BothHandsInvalidSeconds;

        public bool IsFalling => State == ForceState.Falling;
    }

    public interface IGripQueryProvider
    {
        bool TryQueryGrip(Vector3 handPosition, float radius, out GripQueryResult result);
    }

    public interface IForceInputAdapter
    {
        ForceEvaluationInput BuildInput(float deltaTime);
    }

    public interface IForceFallEventSink
    {
        void OnForceFallTriggered(ForceEvaluationResult result);
    }

    public static class ForceEvaluator
    {
        public static ForceEvaluationResult Evaluate(
            ForceEvaluationInput input,
            ref ForceEvaluationMemory memory,
            ForceEvaluationSettings settings)
        {
            settings = settings.Sanitized();

            var deltaTime = Mathf.Max(0f, input.DeltaTime);
            var previousState = memory.PreviousState;
            var left = EvaluateHand(input.LeftHand, input.Body, settings);
            var right = EvaluateHand(input.RightHand, input.Body, settings);
            var state = ResolveState(input.Body, left, right, deltaTime, settings, ref memory);

            var result = new ForceEvaluationResult
            {
                State = state,
                LeftHand = left,
                RightHand = right,
                PrimaryFailureReason = ResolvePrimaryFailure(left, right),
                HasUnstableGrip = (left.IsEffective && !left.IsStableGrip) || (right.IsEffective && !right.IsStableGrip),
                SingleHandGraceExpired = memory.SingleHandInvalidSeconds > settings.SingleHandGraceSeconds,
                FallTriggered = state == ForceState.Falling && previousState != ForceState.Falling,
                SingleHandInvalidSeconds = memory.SingleHandInvalidSeconds,
                BothHandsInvalidSeconds = memory.BothHandsInvalidSeconds
            };

            memory.PreviousState = state;
            return result;
        }

        public static ForceHandEvaluation EvaluateHand(
            HandForceInput hand,
            BodyForceInput body,
            ForceEvaluationSettings settings)
        {
            settings = settings.Sanitized();

            if (!body.AllowsClimbing)
            {
                return ForceHandEvaluation.Failed(ForceHandFailureReason.BodyUnavailable, hand.GripQuality);
            }

            if (hand.ExternalFailureReason != ForceHandFailureReason.None)
            {
                return ForceHandEvaluation.Failed(ForceHandFailureReason.ExternalFailure, hand.GripQuality);
            }

            if (!hand.IsTouching)
            {
                return ForceHandEvaluation.Failed(ForceHandFailureReason.NoContact, hand.GripQuality);
            }

            if (!hand.HasGripCandidate || hand.PointType == ForcePointType.None)
            {
                return ForceHandEvaluation.Failed(ForceHandFailureReason.NoGripCandidate, hand.GripQuality);
            }

            if (hand.PointType == ForcePointType.Obstacle)
            {
                return ForceHandEvaluation.Failed(ForceHandFailureReason.Obstacle, hand.GripQuality);
            }

            if (hand.PointType == ForcePointType.Fake && hand.IsFakeRevealed)
            {
                return ForceHandEvaluation.Failed(ForceHandFailureReason.FakePoint, hand.GripQuality);
            }

            if (hand.Stamina <= settings.StaminaThreshold)
            {
                return ForceHandEvaluation.Failed(ForceHandFailureReason.StaminaDepleted, hand.GripQuality);
            }

            if (hand.GripQuality < settings.MinGripQuality)
            {
                return ForceHandEvaluation.Failed(ForceHandFailureReason.GripQualityTooLow, hand.GripQuality);
            }

            var isStableGrip = hand.GripQuality >= settings.StableGripQuality;
            var staminaMultiplier = ResolveStaminaMultiplier(hand.GripQuality, settings);
            return ForceHandEvaluation.Effective(isStableGrip, hand.GripQuality, staminaMultiplier);
        }

        private static ForceState ResolveState(
            BodyForceInput body,
            ForceHandEvaluation left,
            ForceHandEvaluation right,
            float deltaTime,
            ForceEvaluationSettings settings,
            ref ForceEvaluationMemory memory)
        {
            if (body.IsAlreadyFalling)
            {
                memory.SingleHandInvalidSeconds = 0f;
                memory.BothHandsInvalidSeconds += deltaTime;
                return ForceState.Falling;
            }

            if (left.IsEffective && right.IsEffective)
            {
                memory.SingleHandInvalidSeconds = 0f;
                memory.BothHandsInvalidSeconds = 0f;
                return ForceState.Stable;
            }

            if (!left.IsEffective && !right.IsEffective)
            {
                memory.SingleHandInvalidSeconds = 0f;
                memory.BothHandsInvalidSeconds += deltaTime;
                return memory.BothHandsInvalidSeconds >= settings.BothHandsFallDelaySeconds
                    ? ForceState.Falling
                    : ForceState.BothHandsReleased;
            }

            memory.BothHandsInvalidSeconds = 0f;
            memory.SingleHandInvalidSeconds += deltaTime * ResolveSingleHandInstabilityMultiplier(left, right, settings);

            return left.IsEffective ? ForceState.RightHandReleased : ForceState.LeftHandReleased;
        }

        private static float ResolveSingleHandInstabilityMultiplier(
            ForceHandEvaluation left,
            ForceHandEvaluation right,
            ForceEvaluationSettings settings)
        {
            var effectiveHand = left.IsEffective ? left : right;
            return effectiveHand.IsStableGrip ? 1f : settings.LowQualityInstabilityMultiplier;
        }

        private static float ResolveStaminaMultiplier(float gripQuality, ForceEvaluationSettings settings)
        {
            if (gripQuality >= settings.StableGripQuality)
            {
                return 1f;
            }

            var span = Mathf.Max(0.0001f, settings.StableGripQuality - settings.MinGripQuality);
            var t = Mathf.Clamp01((gripQuality - settings.MinGripQuality) / span);
            return Mathf.Lerp(settings.LowQualityStaminaCostMultiplier, 1f, t);
        }

        private static ForceHandFailureReason ResolvePrimaryFailure(
            ForceHandEvaluation left,
            ForceHandEvaluation right)
        {
            if (!left.IsEffective && left.FailureReason != ForceHandFailureReason.None)
            {
                return left.FailureReason;
            }

            if (!right.IsEffective && right.FailureReason != ForceHandFailureReason.None)
            {
                return right.FailureReason;
            }

            return ForceHandFailureReason.None;
        }
    }

    [Serializable]
    public sealed class DebugHandProbe
    {
        public Transform HandTransform;
        public float QueryRadius = 0.25f;
        public bool IsTouching = true;
        public ForcePointType ManualPointType = ForcePointType.ValidHold;
        [Range(0f, 1f)] public float ManualGripQuality = 1f;
        public bool IsFakeRevealed;
        public float Stamina = 1f;
        public ForceHandFailureReason ExternalFailureReason;
        public string ManualPointId = string.Empty;

        public Vector3 Position => HandTransform != null ? HandTransform.position : Vector3.zero;

        public HandForceInput BuildInput(GripQueryResult grip)
        {
            return HandForceInput.FromGrip(
                IsTouching,
                grip,
                Stamina,
                Position,
                ExternalFailureReason);
        }

        public GripQueryResult BuildManualGrip()
        {
            return GripQueryResult.Candidate(
                ManualPointType,
                ManualGripQuality,
                IsFakeRevealed,
                Vector3.forward,
                ManualPointId,
                ManualPointType.ToString());
        }
    }

    public sealed class ForceSystemDebugDriver : MonoBehaviour, IForceInputAdapter
    {
        [SerializeField] private ForceSystemConfig config;
        [SerializeField] private MonoBehaviour gripQueryProviderSource;
        [SerializeField] private bool useGripQueryProvider;
        [SerializeField] private bool cacheGripCandidates = true;
        [SerializeField] private float gripQueryIntervalSeconds = 0.02f;
        [SerializeField] private DebugHandProbe leftHand = new DebugHandProbe();
        [SerializeField] private DebugHandProbe rightHand = new DebugHandProbe();
        [SerializeField] private BodyForceInput body;
        [SerializeField] private bool logStateChanges;
        [SerializeField] private MonoBehaviour fallEventSinkSource;

        private IGripQueryProvider _gripQueryProvider;
        private IForceFallEventSink _fallEventSink;
        private ForceEvaluationMemory _memory = ForceEvaluationMemory.CreateDefault();
        private ForceEvaluationResult _lastResult;
        private GripQueryResult _cachedLeftGrip = GripQueryResult.None();
        private GripQueryResult _cachedRightGrip = GripQueryResult.None();
        private float _nextGripQueryTime;

        public ForceEvaluationResult LastResult => _lastResult;

        private void Awake()
        {
            ResolveInterfaces();
        }

        private void OnValidate()
        {
            gripQueryIntervalSeconds = Mathf.Max(0f, gripQueryIntervalSeconds);
        }

        private void Update()
        {
            var input = BuildInput(Time.deltaTime);
            var settings = config != null ? config.Settings : ForceEvaluationSettings.CreateDefault();
            var previousState = _lastResult.State;

            _lastResult = ForceEvaluator.Evaluate(input, ref _memory, settings);

            if ((settings.EnableDebugLog || logStateChanges) && _lastResult.State != previousState)
            {
                Debug.Log(
                    $"Force state: {_lastResult.State}, left={_lastResult.LeftHand.IsEffective}/{_lastResult.LeftHand.GripQuality:0.00}, " +
                    $"right={_lastResult.RightHand.IsEffective}/{_lastResult.RightHand.GripQuality:0.00}, reason={_lastResult.PrimaryFailureReason}",
                    this);
            }

            if (_lastResult.FallTriggered)
            {
                _fallEventSink?.OnForceFallTriggered(_lastResult);
            }
        }

        public ForceEvaluationInput BuildInput(float deltaTime)
        {
            ResolveInterfaces();
            RefreshGripCacheIfNeeded();

            return new ForceEvaluationInput
            {
                LeftHand = leftHand.BuildInput(_cachedLeftGrip),
                RightHand = rightHand.BuildInput(_cachedRightGrip),
                Body = body,
                PreviousState = _memory.PreviousState,
                DeltaTime = deltaTime
            };
        }

        public void ResetEvaluation()
        {
            _memory.Reset();
            _lastResult = default;
        }

        private void ResolveInterfaces()
        {
            _gripQueryProvider = gripQueryProviderSource as IGripQueryProvider;
            _fallEventSink = fallEventSinkSource as IForceFallEventSink;
        }

        private void RefreshGripCacheIfNeeded()
        {
            if (!cacheGripCandidates || Time.time >= _nextGripQueryTime)
            {
                _nextGripQueryTime = Time.time + gripQueryIntervalSeconds;
                _cachedLeftGrip = ResolveGrip(leftHand);
                _cachedRightGrip = ResolveGrip(rightHand);
            }
        }

        private GripQueryResult ResolveGrip(DebugHandProbe probe)
        {
            if (useGripQueryProvider && _gripQueryProvider != null &&
                _gripQueryProvider.TryQueryGrip(probe.Position, probe.QueryRadius, out var result))
            {
                return result;
            }

            return probe.BuildManualGrip();
        }
    }
}
