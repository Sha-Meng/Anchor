using Anchor.ForceSystem;
using ClimbGame.Climb3C.Character;
using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using ClimbGame.Climb3C.Feedback;
using ClimbGame.Climb3C.Input;
using ClimbGame.Climb3C.State;
using ClimbGame.Climb3C.UI;
using UnityEngine;

namespace ClimbGame.Climb3C.Gameplay
{
    public enum ClimbState { WaitingForPress, Reaching, Returning, Falling }

    /// <summary>
    /// 本地攀爬者的逻辑层：消费输入，驱动 <see cref="IClimberAvatar"/> 化身，
    /// 所有运行时数据读写集中在 <see cref="GameContext"/> 的 <see cref="ClimberRuntimeState"/> 上
    /// （便于后续联机同步）。左右手交替攀爬状态机、双手中点重心、耐力与布娃娃摔落。
    /// </summary>
    public sealed class ClimbController3D : MonoBehaviour, IClimbStateSource
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
        private ClimbCamera _camera;
        private ClimbCameraConfig _cameraConfig;
        private InputZoneOverlayUI _zoneOverlay;
        private IGripQueryProvider _gripProvider;
        private WallDepthProbe _wallProbe;
        private CapsuleWallResolver _wallResolver;
        private float _bodyWallOffset = 0.4f;
        private ForceEvaluationSettings _forceSettings = ForceEvaluationSettings.CreateDefault();
        private readonly ClimbForceInputAdapter _forceInputAdapter = new ClimbForceInputAdapter();

        private GameContext _ctx;
        private ClimberRuntimeState _s;
        private float _climbZ;

        // 纯表现瞬态（不进 GameContext）
        private Vector2 _lastTouchScreen;
        private bool _showTouchMarker;
        private Vector2 _magnifierScreen;
        private bool _showMagnifier;

        public ClimbState State => _s != null ? _s.State : ClimbState.WaitingForPress;
        public ClimbHand CurrentHand => _s != null ? _s.CurrentHand : ClimbHand.None;
        public Vector3 TorsoCenter => _s != null ? _s.TorsoCenter : Vector3.zero;

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
                IsFalling = _s.State == ClimbState.Falling
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
            _s.ForceMemory = ForceEvaluationMemory.CreateDefault();
            _forceInputAdapter.Configure(_s);

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

            _avatar.SetTorsoCenter(startCenter);
            _avatar.DriveArm(ClimbHand.Left, _s.LeftAnchor, true);
            _avatar.DriveArm(ClimbHand.Right, _s.RightAnchor, true);
        }

        private void Update()
        {
            if (_avatar == null || _s == null) return;
            float dt = Time.deltaTime;
            _showTouchMarker = false;
            _showMagnifier = false;

            if (_s.State == ClimbState.Falling)
            {
                UpdateFalling(dt);
            }
            else
            {
                UpdateClimb(dt);
                UpdateTorsoAndArms();
                // 头部 lookat 跟随攀爬手（伸手/回收时），否则回中立
                DriveHeadLook();
                // 坠落判定交给 SystemValidation 的 ForceEvaluator（逐帧演算，输入来自 GameContext）
                EvaluateForceState(dt);
            }

            if (_showMagnifier)
            {
                Vector3 handWorld = _avatar.GetHandPosition(_s.CurrentHand);
                _magnifier.UpdateLens(handWorld, _magnifierScreen, _haptics.CurrentTier);
            }
            else
            {
                _magnifier.Hide();
            }

            if (_staminaBar != null) _staminaBar.SetRatio(_stamina.Ratio);
            if (_zoneOverlay != null)
            {
                _zoneOverlay.SetActiveSide(_s.CurrentHand);
                _zoneOverlay.SetTouchMarker(_lastTouchScreen, _showTouchMarker);
            }
        }

        private void UpdateClimb(float dt)
        {
            if (_s.State == ClimbState.WaitingForPress)
            {
                bool stable = true;
                if (_s.CurrentHand == ClimbHand.None)
                {
                    if (_input.TryGetAnyNewPress(out ClimbPointer p, out ClimbHand side))
                    {
                        BeginReach(side, p);
                        stable = false;
                    }
                }
                else
                {
                    if (_input.TryGetNewPress(_s.CurrentHand, out ClimbPointer p))
                    {
                        BeginReach(_s.CurrentHand, p);
                        stable = false;
                    }
                }

                if (stable && _s.State == ClimbState.WaitingForPress)
                {
                    _stamina.Recover(dt);
                }
            }

            if (_s.State == ClimbState.Reaching)
            {
                if (_input.TryGetPointerById(_s.TrackedFinger, out ClimbPointer p))
                {
                    // 相对映射：手 = 手起点 +（触点当前位 − 触点起点）。按下瞬间位移为 0，手不跳变。
                    Vector3 touchNow = _projector.Project(p.ScreenPos, out _);
                    Vector3 target = _s.ReachStartHand + (touchNow - _s.ReachStartTouch);
                    // 仅沿 Z 贴合墙面，x/y 由触点映射决定，不改变手臂可达范围
                    if (_wallProbe != null) target = _wallProbe.StickToWall(target, out _);
                    _s.AttackHandCurrent = SmoothTo(_s.AttackHandCurrent, target, _tuning.handFollowLerp);
                    // 平滑插值会让 z 偏离墙面，每帧仅修正 z
                    if (_wallProbe != null) _s.AttackHandCurrent = _wallProbe.StickToWall(_s.AttackHandCurrent, out _);

                    if (p.Phase == ClimbPointerPhase.Ended)
                    {
                        // 松手：用相对映射的最终位（去平滑）做一次吸附判定
                        _s.AttackHandCurrent = target;
                        ResolveRelease();
                    }
                    else
                    {
                        // 持续伸手：只给靠近反馈，不在此吸附（吸附只在松手时判定）
                        _lastTouchScreen = p.ScreenPos;
                        _showTouchMarker = true;
                        _rivets.FindNearestExcluding(target, ExcludeRivet(), out float dist);
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
            return attacking ? _s.AttackHandCurrent : AnchorOf(hand);
        }

        private RivetPoint ExcludeRivet() => _s.CurrentHand == ClimbHand.Left ? _s.LeftRivet : _s.RightRivet;

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
        /// 吸附判定：判定逻辑交给 SystemValidation 的抓握判定接口（IGripQueryProvider.TryQueryGrip），
        /// 其参数（手掌世界位、查询半径）从 GameContext 取。无判定接口时回退到距离判定（供 Demo）。
        /// </summary>
        private bool TryDetermineGrab(out RivetPoint hold)
        {
            Vector3 handPos = _s.AttackHandCurrent;   // 判定参数：来自 GameContext 运行时状态
            float radius = _ctx.GripQueryRadius;        // 判定参数：来自 GameContext

            hold = _rivets.FindNearestExcluding(handPos, ExcludeRivet(), out float dist);
            if (hold == null) return false;

            bool valid;
            if (_gripProvider != null)
            {
                valid = _gripProvider.TryQueryGrip(handPos, radius, out GripQueryResult grip)
                        && grip.HasCandidate
                        && grip.PointType == ForcePointType.ValidHold;
            }
            else
            {
                valid = dist <= _hapticCfg.snapRadius;
            }

            // 判定通过，且吸附目标确实落在查询半径内，才吸附
            return valid && dist <= radius;
        }

        public void SetGripProvider(IGripQueryProvider provider) => _gripProvider = provider;

        public void SetWallProbe(WallDepthProbe probe) => _wallProbe = probe;

        public void SetWallResolver(CapsuleWallResolver resolver) => _wallResolver = resolver;

        public void SetBodyWallOffset(float offset) => _bodyWallOffset = offset;

        public void SetCameraConfig(ClimbCameraConfig cfg) => _cameraConfig = cfg;

        private void DriveHeadLook()
        {
            if (_cameraConfig == null) return;
            bool looking = (_s.State == ClimbState.Reaching || _s.State == ClimbState.Returning)
                           && _s.CurrentHand != ClimbHand.None;
            Vector3 handTarget = looking ? _avatar.GetHandPosition(_s.CurrentHand) : Vector3.zero;
            _avatar.UpdateHeadLook(handTarget, looking, _cameraConfig.neutralForward,
                _cameraConfig.headLookMaxAngle, _cameraConfig.headLookLerp);
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
            // 记录按下瞬间的触点位与手位：手先不动，后续按触点位移相对移动
            _s.ReachStartTouch = _projector.Project(pointer.ScreenPos, out _);
            _s.ReachStartHand = AnchorOf(side);
            _s.AttackHandCurrent = _s.ReachStartHand;
            _s.State = ClimbState.Reaching;
        }

        private void BeginReturn()
        {
            _magnifier.Hide();
            _haptics.ClearProximity();
            _s.State = ClimbState.Returning;
        }

        private void Grab(RivetPoint rivet)
        {
            SetAnchor(_s.CurrentHand, rivet.GrabPosition);
            int rivetId = _rivets.IndexOf(rivet);
            if (_s.CurrentHand == ClimbHand.Left) { _s.LeftRivet = rivet; _s.LeftRivetId = rivetId; }
            else { _s.RightRivet = rivet; _s.RightRivetId = rivetId; }

            _s.AttackHandCurrent = rivet.GrabPosition;
            _haptics.GrabPulse();
            _haptics.ClearProximity();
            _magnifier.Hide();
            _s.CurrentHand = _s.CurrentHand.Other();
            _s.State = ClimbState.WaitingForPress;
        }

        private void BeginFall()
        {
            _magnifier.Hide();
            _haptics.ClearProximity();
            _s.State = ClimbState.Falling;
            _s.FallStartY = _avatar.TorsoWorldPosition.y;
            _s.FallTimer = 0f;

            float down = _fallConfig != null ? _fallConfig.initialDownSpeed : 1.5f;
            float push = _fallConfig != null ? _fallConfig.pushFromWall : 1.2f;
            Vector3 vel = Vector3.down * down + Vector3.back * push;
            _avatar.EnterRagdoll(vel);

            if (_camera != null) _camera.SetFalling(true);
        }

        private void UpdateFalling(float dt)
        {
            _s.FallTimer += dt;
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

            Vector3 mid = (_s.LeftAnchor + _s.RightAnchor) * 0.5f + _tuning.torsoCenterOffset;
            mid.z = _climbZ;
            _s.TorsoCenter = mid;

            _avatar.SetupClimbPose(mid);
            _avatar.DriveArm(ClimbHand.Left, _s.LeftAnchor, true);
            _avatar.DriveArm(ClimbHand.Right, _s.RightAnchor, true);
        }

        public void SetFallDependencies(RagdollFallConfig fallConfig, ClimbCamera camera)
        {
            _fallConfig = fallConfig;
            _camera = camera;
        }

        public void SetZoneOverlay(InputZoneOverlayUI overlay) => _zoneOverlay = overlay;
    }
}
