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
    public sealed class ClimbController3D : MonoBehaviour
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
        private InputZoneOverlayUI _zoneOverlay;

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
                if (_input.TryGetPointerById(_s.TrackedFinger, out ClimbPointer p) && p.Phase != ClimbPointerPhase.Ended)
                {
                    Vector3 target = _projector.Project(p.ScreenPos, out _);
                    _lastTouchScreen = p.ScreenPos;
                    _showTouchMarker = true;
                    _s.AttackHandCurrent = SmoothTo(_s.AttackHandCurrent, target, _tuning.handFollowLerp);

                    // 抓握/靠近检测用触点的实际投影位（而非平滑滞后的手位），提升贴合灵敏度：
                    // 手指落在抓点上即判定，不必等平滑追上。
                    RivetPoint nearest = _rivets.FindNearestExcluding(target, ExcludeRivet(), out float dist);
                    _haptics.UpdateProximity(dist);
                    _magnifierScreen = p.ScreenPos;
                    _showMagnifier = true;
                    _stamina.Drain(dt);

                    if (nearest != null && dist <= _hapticCfg.snapRadius)
                    {
                        Grab(nearest);
                    }
                    else if (_stamina.IsEmpty)
                    {
                        BeginFall();
                    }
                }
                else
                {
                    BeginReturn();
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
                else if (_stamina.IsEmpty)
                {
                    BeginFall();
                }
            }
        }

        private void UpdateTorsoAndArms()
        {
            Vector3 leftGoal = GoalFor(ClimbHand.Left);
            Vector3 rightGoal = GoalFor(ClimbHand.Right);

            Vector3 mid = (leftGoal + rightGoal) * 0.5f + _tuning.torsoCenterOffset;
            mid = ClampTorsoForAnchoredHands(mid);
            _s.TorsoCenter = SmoothTo(_s.TorsoCenter, mid, _tuning.torsoFollowLerp);

            _avatar.SetTorsoCenter(_s.TorsoCenter);
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

        private void BeginReach(ClimbHand side, ClimbPointer pointer)
        {
            _s.CurrentHand = side;
            _s.TrackedFinger = pointer.Id;
            _s.AttackHandCurrent = AnchorOf(side);
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

        public void SetFallDependencies(RagdollFallConfig fallConfig, ClimbCamera camera)
        {
            _fallConfig = fallConfig;
            _camera = camera;
        }

        public void SetZoneOverlay(InputZoneOverlayUI overlay) => _zoneOverlay = overlay;
    }
}
