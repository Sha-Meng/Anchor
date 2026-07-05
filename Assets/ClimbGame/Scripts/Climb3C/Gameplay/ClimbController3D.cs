using Anchor.ForceSystem;
using Anchor.RivetRopeSystem;
using ClimbGame.Climb3C.Character;
using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using ClimbGame.Climb3C.Feedback;
using ClimbGame.Climb3C.Input;
using ClimbGame.Climb3C.State;
using ClimbGame.Climb3C.UI;
using DesignerSpace;
using UnityEngine;

namespace ClimbGame.Climb3C.Gameplay
{
    public enum ClimbState { WaitingForPress, Reaching, Returning, Falling }

    public struct RopeForceConsumerDebugSnapshot
    {
        public bool HasFeedback;
        public bool Consuming;
        public string IgnoreReason;
        public Vector3 AppliedDisplacement;
        public Vector3 SuggestedVelocityCorrection;
        public float TensionStrength;
        public float ConstraintDistance;

        public static RopeForceConsumerDebugSnapshot Inactive(string reason)
        {
            return new RopeForceConsumerDebugSnapshot
            {
                HasFeedback = false,
                Consuming = false,
                IgnoreReason = reason ?? string.Empty,
                AppliedDisplacement = Vector3.zero,
                SuggestedVelocityCorrection = Vector3.zero,
                TensionStrength = 0f,
                ConstraintDistance = 0f
            };
        }
    }

    /// <summary>
    /// 本地攀爬者的逻辑层：消费输入，驱动 <see cref="IClimberAvatar"/> 化身，
    /// 所有运行时数据读写集中在 <see cref="GameContext"/> 的 <see cref="ClimberRuntimeState"/> 上
    /// （便于后续联机同步）。左右手交替攀爬状态机、双手中点重心、耐力与布娃娃摔落。
    /// </summary>
    public sealed class ClimbController3D : MonoBehaviour, IClimbStateSource, IRivetRopeDamageSink, IPlayerHealthStateSource
    {
        private ClimbTuningConfig _tuning;
        private ArmRigConfig _rig;
        private StaminaConfig _staminaCfg;
        private HapticConfig _hapticCfg;
        private RagdollFallConfig _fallConfig;

        private IClimberAvatar _avatar;
        private ClimbStamina _stamina;
        private ClimbTouchInput _input;
        private WallProjector _projector;
        private RivetField _rivets;
        private HapticService _haptics;
        private HandMagnifier _magnifier;
        private StaminaBarUI _staminaBar;
        private HealthBarUI _healthBar;
        private FailurePopupUI _failurePopup;
        private ClimbCamera _camera;
        private ClimbCameraConfig _cameraConfig;
        private PlayerHealthController _health;
        private IGripQueryProvider _gripProvider;
        private WallDepthProbe _wallProbe;
        private CapsuleWallResolver _wallResolver;
        private bool _consumeRopeForceFeedback;
        private RopeForceFeedbackResult _pendingRopeForceFeedback;
        private RopeForceConsumerDebugSnapshot _ropeForceDebug = RopeForceConsumerDebugSnapshot.Inactive("NoFeedback");
        private bool _ropeFallCaught;
        private bool _ropeFallReboundTriggered;
        private float _bodyWallOffset = 0.4f;
        private float _maxHandDistance = 2f;
        private float _handSlipCancelDistance = 0.5f;
        private float _gripMagnetZOffset = 0.1f;
        private float _grabSnapDistanceXY = 0.5f;
        private bool _magnifierEnabled = true;
        private float _ropeFallSpring = 7.5f;
        private float _ropeFallDamping = 1.8f;
        private float _ropeFallImpulse = 0.65f;
        private float _ropeFallImpulseStretch = 0.28f;
        private float _ropeFallMaxVelocityChange = 3.2f;
        private ForceEvaluationSettings _forceSettings = ForceEvaluationSettings.CreateDefault();
        private readonly ClimbForceInputAdapter _forceInputAdapter = new ClimbForceInputAdapter();
        private ControllerMgr _controllerMgr;

        private GameContext _ctx;
        private ClimberRuntimeState _s;
        private float _climbZ;

        // 纯表现瞬态（不进 GameContext）
        private Vector2 _magnifierScreen;
        private bool _showMagnifier;
        private float _smoothedHandZ;
        // 相机视平面映射：起手屏幕点与手所在深度（本地输入用，逐帧把屏幕位移映射到该视平面）
        private Vector2 _reachStartScreen;
        private float _reachDepth;

        public ClimbState State => _s != null ? _s.State : ClimbState.WaitingForPress;
        public ClimbHand CurrentHand => _s != null ? _s.CurrentHand : ClimbHand.None;
        public Vector3 TorsoCenter => _s != null ? _s.TorsoCenter : Vector3.zero;
        public bool IsFailed => _s != null && _s.IsFailed;
        public RopeForceConsumerDebugSnapshot RopeForceDebug => _ropeForceDebug;
        public PlayerHealthSnapshot HealthSnapshot => ReadHealthSnapshot();

        /// <summary>某只手成功抓握锚定时触发，参数为刚锚定的手（相机可据此回到中性机位）。</summary>
        public event System.Action<ClimbHand> HandAnchored;

        /// <summary>手指按下开始伸手（touch 脱离锚点）时触发，参数为伸出/脱离的手。</summary>
        public event System.Action<ClimbHand> HandReachStarted;

        public event System.Action<PlayerHealthSnapshot, RopeFallResolution> PlayerFailed;

        private void OnEnable()
        {
            TrySubscribeControllerMgr();
        }

        private void OnDisable()
        {
            UnsubscribeControllerMgr();
        }

        /// <summary>左手当前锚点世界坐标。</summary>
        public Vector3 LeftAnchor => _s != null ? _s.LeftAnchor : Vector3.zero;

        /// <summary>右手当前锚点世界坐标。</summary>
        public Vector3 RightAnchor => _s != null ? _s.RightAnchor : Vector3.zero;

        /// <summary>双手锚点中点世界坐标（相机 rig 跟随目标）。</summary>
        public Vector3 AnchorMidpoint => (LeftAnchor + RightAnchor) * 0.5f;

        public bool TryGetSnapshot(out ClimbStateSnapshot snapshot)
        {
            if (_s == null || _avatar == null)
            {
                snapshot = default;
                return false;
            }

            snapshot = new ClimbStateSnapshot
            {
                PlayerId = _s.PlayerId,
                State = _s.State,
                CurrentHand = _s.CurrentHand,
                TorsoCenter = _s.TorsoCenter,
                LeftHandPosition = _avatar.GetHandPosition(ClimbHand.Left),
                RightHandPosition = _avatar.GetHandPosition(ClimbHand.Right),
                LeftRivetId = _s.LeftRivetId,
                RightRivetId = _s.RightRivetId,
                StaminaRatio = _s.StaminaRatio,
                IsFalling = _s.State == ClimbState.Falling,
                Health = _s.Health,
                MaxHealth = _s.MaxHealth,
                IsFailed = _s.IsFailed
            };
            return true;
        }

        public void Initialize(
            GameContext context, int playerId,
            ClimbTuningConfig tuning, ArmRigConfig rig, StaminaConfig staminaCfg, HapticConfig hapticCfg,
            IClimberAvatar avatar, ClimbTouchInput input, WallProjector projector,
            RivetField rivets, HapticService haptics, HandMagnifier magnifier, StaminaBarUI staminaBar,
            Vector3 startCenter)
        {
            _ctx = context;
            _s = _ctx.GetOrCreate(playerId, playerId == _ctx.LocalPlayerId);

            _tuning = tuning;
            _rig = rig;
            _staminaCfg = staminaCfg;
            _hapticCfg = hapticCfg;
            _avatar = avatar;
            _input = input;
            _projector = projector;
            _rivets = rivets;
            _haptics = haptics;
            _magnifier = magnifier;
            _staminaBar = staminaBar;

            _stamina = new ClimbStamina(staminaCfg, _s);
            ApplyLevelGlobalStaminaConfig(true);
            TrySubscribeControllerMgr();
            _health = new PlayerHealthController(ReadHealthSnapshot, WriteHealthSnapshot);
            _health.HealthChanged += RefreshHealthUi;
            _health.PlayerFailed += HandlePlayerFailed;
            _health.Reset(playerId.ToString(), PlayerHealthSettings.CreateDefault());
            _s.ForceMemory = ForceEvaluationMemory.CreateDefault();
            _forceInputAdapter.Configure(_s);
            _consumeRopeForceFeedback = false;
            _pendingRopeForceFeedback = default;
            _ropeForceDebug = RopeForceConsumerDebugSnapshot.Inactive("NoFeedback");
            _ropeFallCaught = false;
            _ropeFallReboundTriggered = false;

            _s.State = ClimbState.WaitingForPress;
            _s.CurrentHand = ClimbHand.None;
            _s.LeftRivet = null;
            _s.RightRivet = null;
            _s.LeftRivetId = -1;
            _s.RightRivetId = -1;
            _s.TorsoCenter = startCenter;
            _climbZ = startCenter.z;
            _s.LeftAnchor = ComputeRestAnchor(startCenter, ClimbHand.Left);
            _s.RightAnchor = ComputeRestAnchor(startCenter, ClimbHand.Right);
            _s.AttackHandCurrent = _s.LeftAnchor;
            // 初始不驱动磁点/躯干：仅由 SetInitialGrips 把磁点设到抓点、由 Build 设玩家位置。
        }

        public void ApplyReleaseStaminaPenalty(float maxStaminaRatio)
        {
            if (_stamina == null || _s == null || !_s.IsLocal)
            {
                return;
            }

            _stamina.ConsumeMaxRatio(maxStaminaRatio);
        }

        private void ApplyLevelGlobalStaminaConfig(bool refill)
        {
            if (_stamina == null)
            {
                return;
            }

            LevelMgr levelMgr = LevelMgr.Instance;
            LevelGlobalConfig config = levelMgr != null ? levelMgr.Config : null;
            if (config == null)
            {
                return;
            }

            _stamina.Configure(config.StaminaMax, config.StaminaRecoverPerSecond, refill);
        }

        private void TrySubscribeControllerMgr()
        {
            if (_controllerMgr != null)
            {
                return;
            }

            _controllerMgr = FindObjectOfType<ControllerMgr>();
            if (_controllerMgr != null)
            {
                _controllerMgr.ReleaseStaminaPenaltyRequested += ApplyReleaseStaminaPenalty;
            }
        }

        private void UnsubscribeControllerMgr()
        {
            if (_controllerMgr == null)
            {
                return;
            }

            _controllerMgr.ReleaseStaminaPenaltyRequested -= ApplyReleaseStaminaPenalty;
            _controllerMgr = null;
        }

        private void Update()
        {
            if (_avatar == null || _s == null) return;
            ApplyLevelGlobalStaminaConfig(false);
            float dt = Time.deltaTime;
            _showMagnifier = false;

            if (_s.IsFailed)
            {
                _magnifier.Hide();
                if (_staminaBar != null) _staminaBar.SetVisible(false);
                DriveCameraFollow();
                return;
            }

            if (_s.State == ClimbState.Falling)
            {
                UpdateFalling(dt);
            }
            else
            {
                UpdateClimb(dt);
                UpdateTorsoAndArms();
                // 坠落判定交给 SystemValidation 的 ForceEvaluator（逐帧演算，输入来自 GameContext）
                EvaluateForceState(dt);
            }

            // 相机跟随"未参与攀爬的那只手"（摔落时跟随躯干）
            DriveCameraFollow();

            if (_magnifierEnabled && _showMagnifier)
            {
                Vector3 handWorld = _avatar.GetHandPosition(_s.CurrentHand);
                _magnifier.UpdateLens(handWorld, _magnifierScreen, _haptics.CurrentTier);
            }
            else
            {
                _magnifier.Hide();
            }

            if (_staminaBar != null)
            {
                // 圆环体力条在攀爬动作（伸手/回收）或耐力未满（恢复中）时出现；
                // 耐力满且空闲、或摔落时隐藏。
                bool climbing = _s.State == ClimbState.Reaching || _s.State == ClimbState.Returning;
                bool show = _s.State != ClimbState.Falling && (climbing || _stamina.Ratio < 0.999f);
                _staminaBar.SetVisible(show);
                if (show)
                {
                    Vector3 handsMid = (_avatar.GetHandPosition(ClimbHand.Left) + _avatar.GetHandPosition(ClimbHand.Right)) * 0.5f;
                    _staminaBar.SetWorldAnchor(handsMid);
                    _staminaBar.SetRatio(_stamina.Ratio);
                }
            }
        }

        private void UpdateClimb(float dt)
        {
            if (_s.State == ClimbState.WaitingForPress)
            {
                // 不分左右区：任意 touch 起手，按起点离哪只手更近来决定移动哪只手。
                if (_input.TryGetAnyNewPress(out ClimbPointer p))
                {
                    BeginReach(NearestHandToScreenPoint(p.ScreenPos), p);
                }
                else
                {
                    _stamina.Recover(dt);
                }
            }

            if (_s.State == ClimbState.Reaching)
            {
                if (_input.TryGetPointerById(_s.TrackedFinger, out ClimbPointer p))
                {
                    // 相机视平面映射：把屏幕位移换算到"手当前深度"的相机视平面（垂直于相机前向），
                    // 使 2D 拖动与 3D 手位在画面中同向、等比跟随。手 = 起手真实手位 + 视平面位移。
                    Vector3 worldStart = _projector.ScreenToWorldAtDepth(_reachStartScreen, _reachDepth);
                    Vector3 worldNow = _projector.ScreenToWorldAtDepth(p.ScreenPos, _reachDepth);
                    Vector3 mapped = _s.ReachStartHand + (worldNow - worldStart);
                    // 磁点真实位置：受两手最大距离约束，超出边界则夹取到边界。
                    Vector3 commanded = ClampToPartnerReach(mapped);

                    // touch 目标位 与 磁点真实位置 的差值超过阈值 → 取消本次 touch（放弃伸手，手回到原位）。
                    if ((mapped - commanded).sqrMagnitude > _handSlipCancelDistance * _handSlipCancelDistance)
                    {
                        BeginReturn();
                        return;
                    }

                    // 3D 平滑跟随（相机视平面内）；逼近到位即吸附，touch 静止则完全静止不抖。
                    Vector3 follow = SmoothTo(_s.AttackHandCurrent, commanded, GetHandFollowLerp());
                    if ((follow - commanded).sqrMagnitude < 1e-6f) follow = commanded;
                    // 贴墙开启（stickHandToWall）时才按 +Z 采样墙面 z；关闭则保持视平面跟随的 z。
                    _s.AttackHandCurrent = SampleWallZFromHand(follow);

                    if (p.Phase == ClimbPointerPhase.Ended)
                    {
                        // 松手：落到磁点真实位置（去平滑滞后），贴墙开启时再采样墙面 z 做吸附判定
                        _s.AttackHandCurrent = SampleWallZFromHand(commanded);
                        ResolveRelease();
                    }
                    else
                    {
                        // 持续伸手：只给靠近反馈（同样用 xy 投影距离），不在此吸附（吸附只在松手时判定）
                        _rivets.FindNearestExcludingXY(_s.AttackHandCurrent, ExcludeRivet(), out float dist);
                        _haptics.UpdateProximity(dist);
                        _magnifierScreen = p.ScreenPos;
                        _showMagnifier = true;
                        _stamina.Drain(dt);
                    }
                }
                else
                {
                    // 触点丢失，按松手处理
                    ResolveRelease();
                }
            }
            else if (_s.State == ClimbState.Returning)
            {
                Vector3 anchor = AnchorOf(_s.CurrentHand);
                _s.AttackHandCurrent = SmoothTo(_s.AttackHandCurrent, anchor, _tuning.handReturnLerp);
                _stamina.Drain(dt, _staminaCfg.abandonDrainMultiplier);
                _magnifier.Hide();
                _haptics.ClearProximity();

                if ((_s.AttackHandCurrent - anchor).sqrMagnitude < 0.0025f)
                {
                    _s.State = ClimbState.WaitingForPress;
                }
            }
        }

        private void UpdateTorsoAndArms()
        {
            Vector3 leftGoal = GoalFor(ClimbHand.Left);
            Vector3 rightGoal = GoalFor(ClimbHand.Right);

            Vector3 mid = (leftGoal + rightGoal) * 0.5f + _tuning.torsoCenterOffset;
            mid = ClampTorsoForAnchoredHands(mid);
            // 防穿模：身体沿"背离墙面方向"退到抓点平面前方一个固定距离（确定性，不依赖射线，
            // 避免命中无关碰撞体把角色拽飞）。手臂再从身体前方伸回墙面抓点。
            Vector3 wallForward = _cameraConfig != null && _cameraConfig.neutralForward.sqrMagnitude > 1e-6f
                ? _cameraConfig.neutralForward.normalized
                : Vector3.forward;
            mid -= wallForward * _bodyWallOffset;
            mid = ApplyRopeForceFeedback(mid);
            _s.TorsoCenter = SmoothTo(_s.TorsoCenter, mid, _tuning.torsoFollowLerp);

            // 先按目标中心摆好胶囊体，再做胶囊体 vs 墙体去穿模：
            // 若发生穿插，沿碰撞法线把角色推出贴合墙面，避免胶囊体陷入墙体。
            _avatar.SetTorsoCenter(_s.TorsoCenter);
            if (_wallResolver != null)
            {
                Vector3 resolved = _wallResolver.Resolve(_s.TorsoCenter);
                if ((resolved - _s.TorsoCenter).sqrMagnitude > 1e-8f)
                {
                    _s.TorsoCenter = resolved;
                    _avatar.SetTorsoCenter(_s.TorsoCenter);
                }
            }

            _avatar.DriveArm(ClimbHand.Left, leftGoal, true);
            _avatar.DriveArm(ClimbHand.Right, rightGoal, true);
        }

        private Vector3 GoalFor(ClimbHand hand)
        {
            bool attacking = hand == _s.CurrentHand &&
                             (_s.State == ClimbState.Reaching || _s.State == ClimbState.Returning);
            // 吸附在铆钉上的手：磁点位置沿 -Z 偏移（可配置）；伸手中的手不偏移。
            return attacking ? _s.AttackHandCurrent : GripMagnetPos(AnchorOf(hand));
        }

        /// <summary>把锚定手的磁点目标沿 -Z 偏移（吸附在铆钉时用），偏移量可配置。</summary>
        private Vector3 GripMagnetPos(Vector3 anchor)
        {
            anchor.z -= _gripMagnetZOffset;
            return anchor;
        }

        public void SetGripMagnetZOffset(float offset) => _gripMagnetZOffset = offset;

        public void ConfigureHealthUi(HealthBarUI healthBar, FailurePopupUI failurePopup)
        {
            _healthBar = healthBar;
            _failurePopup = failurePopup;
            RefreshHealthUi(ReadHealthSnapshot());
        }

        public void SetRopeForceFeedbackEnabled(bool enabled)
        {
            _consumeRopeForceFeedback = enabled;
            if (!enabled)
            {
                _pendingRopeForceFeedback = default;
                _ropeForceDebug = RopeForceConsumerDebugSnapshot.Inactive("ConsumerDisabled");
                _ropeFallCaught = false;
                _ropeFallReboundTriggered = false;
            }
        }

        public void SetRopeFallFeedbackSettings(
            float spring,
            float damping,
            float impulse,
            float impulseStretch,
            float maxVelocityChange)
        {
            _ropeFallSpring = Mathf.Max(0f, spring);
            _ropeFallDamping = Mathf.Max(0f, damping);
            _ropeFallImpulse = Mathf.Max(0f, impulse);
            _ropeFallImpulseStretch = Mathf.Max(0.01f, impulseStretch);
            _ropeFallMaxVelocityChange = Mathf.Max(0.1f, maxVelocityChange);
        }

        public void ConsumeRopeForceFeedback(RopeForceFeedbackResult feedback)
        {
            _pendingRopeForceFeedback = feedback;
        }

        public void ClearRopeForceFeedback(string reason = "NoFeedback")
        {
            _pendingRopeForceFeedback = default;
            _ropeForceDebug = RopeForceConsumerDebugSnapshot.Inactive(reason);
        }

        private Vector3 ApplyRopeForceFeedback(Vector3 targetTorso)
        {
            if (!_consumeRopeForceFeedback)
            {
                _ropeForceDebug = RopeForceConsumerDebugSnapshot.Inactive("ConsumerDisabled");
                return targetTorso;
            }

            if (!_pendingRopeForceFeedback.IsActive)
            {
                _ropeForceDebug = RopeForceConsumerDebugSnapshot.Inactive(_pendingRopeForceFeedback.Reason.ToString());
                return targetTorso;
            }

            if (_s.State == ClimbState.Falling)
            {
                _ropeForceDebug = RopeForceConsumerDebugSnapshot.Inactive("HandledByFalling");
                return targetTorso;
            }

            var direction = _pendingRopeForceFeedback.TensionDirection;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                _ropeForceDebug = RopeForceConsumerDebugSnapshot.Inactive("InvalidDirection");
                return targetTorso;
            }

            var dt = Mathf.Max(0.0001f, Time.deltaTime);
            var suggestedDisplacement = _pendingRopeForceFeedback.SuggestedVelocityCorrection * dt;
            var maxDisplacement = Mathf.Max(0.02f, _pendingRopeForceFeedback.ConstraintDistance);
            var applied = Vector3.ClampMagnitude(suggestedDisplacement, maxDisplacement);

            _ropeForceDebug = new RopeForceConsumerDebugSnapshot
            {
                HasFeedback = true,
                Consuming = applied.sqrMagnitude > 0.000001f,
                IgnoreReason = applied.sqrMagnitude > 0.000001f ? string.Empty : "ZeroCorrection",
                AppliedDisplacement = applied,
                SuggestedVelocityCorrection = _pendingRopeForceFeedback.SuggestedVelocityCorrection,
                TensionStrength = _pendingRopeForceFeedback.TensionStrength,
                ConstraintDistance = _pendingRopeForceFeedback.ConstraintDistance
            };

            return targetTorso + applied;
        }

        private RivetPoint ExcludeRivet() => _s.CurrentHand == ClimbHand.Left ? _s.LeftRivet : _s.RightRivet;

        /// <summary>把屏幕触点投影到世界，返回 x/y 平面上离它更近的那只手。</summary>
        private ClimbHand NearestHandToScreenPoint(Vector2 screenPos)
        {
            Vector3 touchWorld = _projector.Project(screenPos, out _);
            Vector3 lh = _avatar.GetHandPosition(ClimbHand.Left);
            Vector3 rh = _avatar.GetHandPosition(ClimbHand.Right);
            float dl = SqrDistXY(touchWorld, lh);
            float dr = SqrDistXY(touchWorld, rh);
            return dl <= dr ? ClimbHand.Left : ClimbHand.Right;
        }

        private static float SqrDistXY(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            return dx * dx + dy * dy;
        }

        private float GetHandFollowLerp()
        {
            if (_tuning == null) return 22f;
            if (_input != null && _input.IsUsingTouchPath)
            {
                return _tuning.mobileHandFollowLerp;
            }

            return _tuning.handFollowLerp;
        }

        private bool UseMobileWallZSmooth()
        {
            return _input != null && _input.IsUsingTouchPath && _tuning != null && _tuning.mobileWallZSmooth > 0f;
        }

        /// <summary>
        /// 从手当前 (x, y) 沿 +Z 射线求与墙体的交点作为 z 落点；未命中则保持原 z。
        /// z 始终对应手实际所在的 x/y，避免 z 与 x/y 错位振荡。
        /// </summary>
        private Vector3 SampleWallZFromHand(Vector3 hand)
        {
            if (_wallProbe != null && _wallProbe.TrySampleSurfaceZ(hand.x, hand.y, out float surfaceZ))
            {
                if (UseMobileWallZSmooth())
                {
                    float t = 1f - Mathf.Exp(-_tuning.mobileWallZSmooth * Time.deltaTime);
                    _smoothedHandZ = Mathf.Lerp(_smoothedHandZ, surfaceZ, t);
                    hand.z = _smoothedHandZ;
                }
                else
                {
                    hand.z = surfaceZ;
                }
            }
            return hand;
        }

        /// <summary>把攻击手目标夹取到另一只（锚定）手的 maxHandDistance 范围内，作为磁点真实位置。</summary>
        private Vector3 ClampToPartnerReach(Vector3 target)
        {
            if (_maxHandDistance <= 0f || _s.CurrentHand == ClimbHand.None) return target;
            Vector3 partner = AnchorOf(_s.CurrentHand.Other());
            Vector3 d = target - partner;
            float m = d.magnitude;
            if (m > _maxHandDistance && m > 1e-4f) target = partner + d * (_maxHandDistance / m);
            return target;
        }

        public void SetMaxHandDistance(float distance) => _maxHandDistance = distance;

        /// <summary>touch 目标位与磁点真实位置的差值超过该值时，取消本次 touch。</summary>
        public void SetHandSlipCancelDistance(float distance) => _handSlipCancelDistance = distance;

        /// <summary>放大镜总开关：关闭后不显示放大镜（保留组件，随时可再启用）。</summary>
        public void SetMagnifierEnabled(bool enabled) => _magnifierEnabled = enabled;

        private Vector3 ClampTorsoForAnchoredHands(Vector3 torso)
        {
            torso = ClampForHandIfAnchored(torso, ClimbHand.Left);
            torso = ClampForHandIfAnchored(torso, ClimbHand.Right);
            return torso;
        }

        private Vector3 ClampForHandIfAnchored(Vector3 torso, ClimbHand hand)
        {
            bool attacking = hand == _s.CurrentHand &&
                             (_s.State == ClimbState.Reaching || _s.State == ClimbState.Returning);
            if (attacking) return torso;

            Vector3 rivet = AnchorOf(hand);
            float sign = hand == ClimbHand.Left ? -1f : 1f;
            Vector3 shoulderOffset = new Vector3(sign * _rig.shoulderHalfWidth, _rig.shoulderVerticalOffset, 0f);
            Vector3 center = rivet - shoulderOffset;
            float reach = _rig.TotalArmLength * _rig.maxReachRatio * 0.97f;
            Vector3 d = torso - center;
            float m = d.magnitude;
            if (m > reach && m > 1e-4f) torso = center + d * (reach / m);
            return torso;
        }

        /// <summary>松手瞬间：做一次吸附判定，成功则抓住对应抓点，失败则手回到原位。</summary>
        private void ResolveRelease()
        {
            if (_s.State != ClimbState.Reaching) return;
            if (TryDetermineGrab(out RivetPoint hold))
            {
                Grab(hold);
            }
            else
            {
                BeginReturn();
            }
        }

        /// <summary>
        /// 攀附判定：直接在攀爬 3C 系统内完成。取当前移动手 xy 最近的吸附点（ScatterAnchor/铆钉），
        /// 若两者 xy 投影距离小于阈值 <see cref="_grabSnapDistanceXY"/> 即判定吸附成功。
        /// </summary>
        private bool TryDetermineGrab(out RivetPoint hold)
        {
            Vector3 handPos = _s.AttackHandCurrent;

            // 只按 xy 投影选取最近吸附点（去掉 z 影响），作为吸附目标
            hold = _rivets.FindNearestExcludingXY(handPos, ExcludeRivet(), out float distXY);
            if (hold == null) return false;

            bool grabbed = distXY <= _grabSnapDistanceXY;
            return grabbed;
        }

        public void SetGrabSnapDistanceXY(float distance) => _grabSnapDistanceXY = distance;

        public void SetGripProvider(IGripQueryProvider provider) => _gripProvider = provider;

        public void SetWallProbe(WallDepthProbe probe) => _wallProbe = probe;

        public void SetWallResolver(CapsuleWallResolver resolver) => _wallResolver = resolver;

        public void SetBodyWallOffset(float offset) => _bodyWallOffset = offset;

        public void SetCameraConfig(ClimbCameraConfig cfg) => _cameraConfig = cfg;

        /// <summary>相机跟随未参与攀爬的手：伸手/回收/等待时跟随非攀爬手，摔落时跟随躯干，起始未定手时取双手中点。</summary>
        private void DriveCameraFollow()
        {
            if (_camera == null) return;
            Vector3 followPos;
            if (_s.State == ClimbState.Falling)
            {
                followPos = _avatar.TorsoWorldPosition;
            }
            else if (_s.CurrentHand != ClimbHand.None)
            {
                followPos = _avatar.GetHandPosition(_s.CurrentHand.Other());
            }
            else
            {
                followPos = (_avatar.GetHandPosition(ClimbHand.Left) + _avatar.GetHandPosition(ClimbHand.Right)) * 0.5f;
            }
            _camera.SetFollowTarget(followPos);
        }

        public void SetForceSettings(ForceEvaluationSettings settings) => _forceSettings = settings.Sanitized();

        /// <summary>
        /// 坠落判定：把当前双手抓握/耐力状态（取自 GameContext）构建为 ForceEvaluationInput，
        /// 交给 SystemValidation 的 ForceEvaluator 演算；当它判定切入 Falling 时触发摔落。
        /// </summary>
        private void EvaluateForceState(float dt)
        {
            var input = _forceInputAdapter.BuildInput(dt);
            var result = ForceEvaluator.Evaluate(input, ref _s.ForceMemory, _forceSettings);
            if (result.FallTriggered)
            {
                BeginFall();
            }
        }

        private void BeginReach(ClimbHand side, ClimbPointer pointer)
        {
            _s.CurrentHand = side;
            _s.TrackedFinger = pointer.Id;
            // 起手瞬间手保持在"真实位置"（而非锚点），避免跳变；仅进入攀爬状态。
            // 记录起手屏幕点与手所在的相机深度，后续按屏幕位移在该视平面内相对移动。
            Vector3 handStart = _avatar.GetHandPosition(side);
            _s.ReachStartHand = handStart;
            _s.AttackHandCurrent = handStart;
            _reachStartScreen = pointer.ScreenPos;
            _reachDepth = _projector.DepthOf(handStart);
            _smoothedHandZ = handStart.z;
            _s.State = ClimbState.Reaching;
            HandReachStarted?.Invoke(side);
        }

        private void BeginReturn()
        {
            _magnifier.Hide();
            _haptics.ClearProximity();
            _s.State = ClimbState.Returning;
        }

        private void Grab(RivetPoint rivet)
        {
            ClimbHand anchoredHand = _s.CurrentHand;
            SetAnchor(_s.CurrentHand, rivet.GrabPosition);
            int rivetId = _rivets.IndexOf(rivet);
            if (_s.CurrentHand == ClimbHand.Left) { _s.LeftRivet = rivet; _s.LeftRivetId = rivetId; }
            else { _s.RightRivet = rivet; _s.RightRivetId = rivetId; }

            _s.AttackHandCurrent = rivet.GrabPosition;
            _haptics.GrabPulse();
            _haptics.ClearProximity();
            _magnifier.Hide();
            // 不再强制左右交替：抓稳后置空当前手，下次 touch 就近选手。
            _s.CurrentHand = ClimbHand.None;
            _s.State = ClimbState.WaitingForPress;
            HandAnchored?.Invoke(anchoredHand);
        }

        private void BeginFall()
        {
            if (_s.IsFailed)
            {
                return;
            }

            _magnifier.Hide();
            _haptics.ClearProximity();
            _s.State = ClimbState.Falling;
            _s.FallStartY = _avatar.TorsoWorldPosition.y;
            _s.FallTimer = 0f;
            _ropeFallCaught = false;
            _ropeFallReboundTriggered = false;

            float down = _fallConfig != null ? _fallConfig.initialDownSpeed : 1.5f;
            float push = _fallConfig != null ? _fallConfig.pushFromWall : 1.2f;
            Vector3 vel = Vector3.down * down + Vector3.back * push;
            _avatar.EnterRagdoll(vel);

            if (_camera != null) _camera.SetFalling(true);
        }

        private void UpdateFalling(float dt)
        {
            _s.FallTimer += dt;
            ApplyRopeFallFeedback(dt);
            float torsoY = _avatar.TorsoWorldPosition.y;
            float dropped = _s.FallStartY - torsoY;

            float fallDistance = _staminaCfg != null ? _staminaCfg.fallDistance : 2.2f;
            float maxSeconds = _staminaCfg != null ? _staminaCfg.maxFallSeconds : 1.5f;

            if (dropped >= fallDistance || _s.FallTimer >= maxSeconds)
            {
                Land(torsoY);
            }
        }

        private void Land(float torsoY)
        {
            if (_s.IsFailed)
            {
                return;
            }

            Vector3 landCenter = new Vector3(_s.TorsoCenter.x, torsoY, _climbZ);
            bool snap = _fallConfig == null || _fallConfig.snapToNearestRivetOnLand;
            if (snap)
            {
                RivetPoint below = _rivets.FindNearestBelow(
                    new Vector3(_s.TorsoCenter.x, torsoY, _climbZ), _s.FallStartY, out _);
                if (below != null)
                {
                    landCenter = new Vector3(below.GrabPosition.x, below.GrabPosition.y + 0.1f, _climbZ);
                }
            }

            _s.TorsoCenter = landCenter;
            _s.LeftAnchor = ComputeRestAnchor(landCenter, ClimbHand.Left);
            _s.RightAnchor = ComputeRestAnchor(landCenter, ClimbHand.Right);
            _s.AttackHandCurrent = _s.LeftAnchor;

            _avatar.SetupClimbPose(landCenter);
            _avatar.DriveArm(ClimbHand.Left, _s.LeftAnchor, true);
            _avatar.DriveArm(ClimbHand.Right, _s.RightAnchor, true);

            _stamina.ResetToRatio(_staminaCfg != null ? _staminaCfg.recoverOnLandRatio : 0.6f);
            _s.ForceMemory = ForceEvaluationMemory.CreateDefault();
            _s.LeftRivet = null;
            _s.RightRivet = null;
            _s.LeftRivetId = -1;
            _s.RightRivetId = -1;
            _s.CurrentHand = ClimbHand.None;
            _s.State = ClimbState.WaitingForPress;
            if (_camera != null) _camera.SetFalling(false);
            _ropeFallCaught = false;
            _ropeFallReboundTriggered = false;
        }

        public void OnRivetRopeFallResolved(RopeFallResolution resolution)
        {
            if (_health == null || _s == null || _s.IsFailed)
            {
                return;
            }

            _health.ApplyFallDamage(resolution);
        }

        private PlayerHealthSnapshot ReadHealthSnapshot()
        {
            if (_s == null)
            {
                return default;
            }

            return new PlayerHealthSnapshot
            {
                PlayerId = _s.PlayerId.ToString(),
                CurrentHealth = _s.Health,
                MaxHealth = _s.MaxHealth,
                IsFailed = _s.IsFailed,
                LastDamage = _s.LastDamage,
                LastDamageReason = _s.LastDamageReason
            };
        }

        private void WriteHealthSnapshot(PlayerHealthSnapshot snapshot)
        {
            if (_s == null)
            {
                return;
            }

            _s.Health = Mathf.Clamp(snapshot.CurrentHealth, 0f, Mathf.Max(1f, snapshot.MaxHealth));
            _s.MaxHealth = Mathf.Max(1f, snapshot.MaxHealth);
            _s.IsFailed = snapshot.IsFailed;
            _s.LastDamage = Mathf.Max(0f, snapshot.LastDamage);
            _s.LastDamageReason = snapshot.LastDamageReason ?? string.Empty;
        }

        private void RefreshHealthUi(PlayerHealthSnapshot snapshot)
        {
            if (_healthBar != null)
            {
                _healthBar.Refresh(snapshot);
            }
        }

        private void HandlePlayerFailed(PlayerHealthSnapshot snapshot, RopeFallResolution resolution)
        {
            _failurePopup?.Show($"伤害 {snapshot.LastDamage:0} / {snapshot.LastDamageReason}");
            if (_staminaBar != null) _staminaBar.SetVisible(false);
            _magnifier.Hide();
            PlayerFailed?.Invoke(snapshot, resolution);
        }

        private void ApplyRopeFallFeedback(float dt)
        {
            if (!_consumeRopeForceFeedback)
            {
                _ropeForceDebug = RopeForceConsumerDebugSnapshot.Inactive("ConsumerDisabled");
                return;
            }

            if (!_pendingRopeForceFeedback.IsActive)
            {
                _ropeForceDebug = RopeForceConsumerDebugSnapshot.Inactive(_pendingRopeForceFeedback.Reason.ToString());
                return;
            }

            if (_pendingRopeForceFeedback.Reason != RopeForceFeedbackReason.Taut)
            {
                _ropeForceDebug = RopeForceConsumerDebugSnapshot.Inactive("FallSlack");
                return;
            }

            var direction = _pendingRopeForceFeedback.TensionDirection;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                _ropeForceDebug = RopeForceConsumerDebugSnapshot.Inactive("InvalidDirection");
                return;
            }

            direction.Normalize();
            var stretch = Mathf.Max(0f, _pendingRopeForceFeedback.ConstraintDistance);
            if (stretch <= 0.0001f)
            {
                _ropeForceDebug = RopeForceConsumerDebugSnapshot.Inactive("ZeroStretch");
                return;
            }

            var dampingScale = 1f / (1f + _ropeFallDamping * Mathf.Max(0.0001f, dt));
            var velocityChange = direction * stretch * _ropeFallSpring * Mathf.Max(0.0001f, dt) * dampingScale;
            if (!_ropeFallReboundTriggered && stretch >= _ropeFallImpulseStretch)
            {
                _ropeFallReboundTriggered = true;
                velocityChange += direction * _ropeFallImpulse;
            }

            velocityChange = Vector3.ClampMagnitude(velocityChange, _ropeFallMaxVelocityChange);
            _avatar.AddRagdollVelocityChange(velocityChange);
            _ropeFallCaught = true;

            _ropeForceDebug = new RopeForceConsumerDebugSnapshot
            {
                HasFeedback = true,
                Consuming = velocityChange.sqrMagnitude > 0.000001f,
                IgnoreReason = velocityChange.sqrMagnitude > 0.000001f ? string.Empty : "ZeroCorrection",
                AppliedDisplacement = Vector3.zero,
                SuggestedVelocityCorrection = velocityChange,
                TensionStrength = _pendingRopeForceFeedback.TensionStrength,
                ConstraintDistance = stretch
            };
        }

        private Vector3 ComputeRestAnchor(Vector3 center, ClimbHand hand)
        {
            float sign = hand == ClimbHand.Left ? -1f : 1f;
            Vector3 shoulder = center + new Vector3(sign * _rig.shoulderHalfWidth, _rig.shoulderVerticalOffset, 0f);
            Vector3 off = _rig.restHandOffset;
            return shoulder + new Vector3(off.x * sign, off.y, off.z);
        }

        private Vector3 AnchorOf(ClimbHand hand) => hand == ClimbHand.Left ? _s.LeftAnchor : _s.RightAnchor;

        private void SetAnchor(ClimbHand hand, Vector3 pos)
        {
            if (hand == ClimbHand.Left) _s.LeftAnchor = pos;
            else _s.RightAnchor = pos;
        }

        private static Vector3 SmoothTo(Vector3 current, Vector3 target, float lerpSpeed)
        {
            if (lerpSpeed <= 0f) return target;
            float t = 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime);
            return Vector3.Lerp(current, target, t);
        }

        /// <summary>
        /// 让角色一开始就双手分别抓在指定铆钉上（而非默认位起攀）。
        /// 躯干落在两抓点中点，双手锚定这两颗铆钉；当前手置空，等待玩家先触发一侧。
        /// </summary>
        public void SetInitialGrips(RivetPoint leftRivet, RivetPoint rightRivet)
        {
            if (_s == null) return;

            if (leftRivet != null)
            {
                _s.LeftAnchor = leftRivet.GrabPosition;
                _s.LeftRivet = leftRivet;
                _s.LeftRivetId = _rivets.IndexOf(leftRivet);
            }
            if (rightRivet != null)
            {
                _s.RightAnchor = rightRivet.GrabPosition;
                _s.RightRivet = rightRivet;
                _s.RightRivetId = _rivets.IndexOf(rightRivet);
            }

            _s.CurrentHand = ClimbHand.None;
            _s.State = ClimbState.WaitingForPress;
            _s.AttackHandCurrent = _s.LeftAnchor;
            _s.TorsoCenter = (_s.LeftAnchor + _s.RightAnchor) * 0.5f;

            // 初始只设置两个 HandMagnet 到抓点（含 -Z 吸附偏移）；玩家位置由 avatar 设置，其余不设。
            _avatar.DriveArm(ClimbHand.Left, GripMagnetPos(_s.LeftAnchor), true);
            _avatar.DriveArm(ClimbHand.Right, GripMagnetPos(_s.RightAnchor), true);
        }

        public void SetFallDependencies(RagdollFallConfig fallConfig, ClimbCamera camera)
        {
            _fallConfig = fallConfig;
            _camera = camera;
        }

    }
}
