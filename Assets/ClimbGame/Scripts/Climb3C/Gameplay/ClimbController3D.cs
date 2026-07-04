using ClimbGame.Climb3C.Character;
using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using ClimbGame.Climb3C.Feedback;
using ClimbGame.Climb3C.Input;
using ClimbGame.Climb3C.UI;
using UnityEngine;

namespace ClimbGame.Climb3C.Gameplay
{
    public enum ClimbState { WaitingForPress, Reaching, Returning, Falling }

    /// <summary>
    /// 攀爬 3C 编排：串起输入区、触点映射、手部 IK、铆钉靠近度震动、放大镜、
    /// 躯干双手中点重心、耐力条与布娃娃摔落。左右手交替攀爬的核心状态机。
    /// </summary>
    public sealed class ClimbController3D : MonoBehaviour
    {
        private ClimbTuningConfig _tuning;
        private ArmRigConfig _rig;
        private StaminaConfig _staminaCfg;

        private ClimbCharacter _character;
        private ClimbStamina _stamina;
        private ClimbTouchInput _input;
        private WallProjector _projector;
        private RivetField _rivets;
        private HapticService _haptics;
        private HandMagnifier _magnifier;
        private HapticConfig _hapticCfg;
        private StaminaBarUI _staminaBar;
        private RagdollFallConfig _fallConfig;
        private ClimbCamera _camera;
        private InputZoneOverlayUI _zoneOverlay;

        private Vector2 _lastTouchScreen;
        private bool _showTouchMarker;
        private Vector2 _magnifierScreen;
        private bool _showMagnifier;

        private ClimbState _state = ClimbState.WaitingForPress;
        private ClimbHand _currentHand = ClimbHand.None;
        private int _trackedFinger;

        private Vector3 _leftAnchor;
        private Vector3 _rightAnchor;
        private Vector3 _attackHandCurrent;
        private Vector3 _torsoCenter;
        private float _climbZ;

        // 每只手当前抓着的铆钉（null=停在默认位）。伸手时排除它，避免"原地重复抓同一颗"。
        private RivetPoint _leftRivet;
        private RivetPoint _rightRivet;
        private RivetPoint _excludeRivet;

        private float _fallStartY;
        private float _fallTimer;

        public ClimbState State => _state;
        public ClimbHand CurrentHand => _currentHand;
        public Vector3 TorsoCenter => _torsoCenter;

        public void Initialize(
            ClimbTuningConfig tuning, ArmRigConfig rig, StaminaConfig staminaCfg, HapticConfig hapticCfg,
            ClimbCharacter character, ClimbStamina stamina, ClimbTouchInput input, WallProjector projector,
            RivetField rivets, HapticService haptics, HandMagnifier magnifier, StaminaBarUI staminaBar,
            Vector3 startCenter)
        {
            _tuning = tuning;
            _rig = rig;
            _staminaCfg = staminaCfg;
            _hapticCfg = hapticCfg;
            _character = character;
            _stamina = stamina;
            _input = input;
            _projector = projector;
            _rivets = rivets;
            _haptics = haptics;
            _magnifier = magnifier;
            _staminaBar = staminaBar;

            _torsoCenter = startCenter;
            _climbZ = startCenter.z;
            _leftAnchor = ComputeRestAnchor(startCenter, ClimbHand.Left);
            _rightAnchor = ComputeRestAnchor(startCenter, ClimbHand.Right);
            _attackHandCurrent = _leftAnchor;

            _character.SetTorsoCenter(startCenter);
            _character.DriveArm(ClimbHand.Left, _leftAnchor);
            _character.DriveArm(ClimbHand.Right, _rightAnchor);
        }

        private void Update()
        {
            if (_character == null) return;
            float dt = Time.deltaTime;
            _showTouchMarker = false;
            _showMagnifier = false;

            if (_state == ClimbState.Falling)
            {
                UpdateFalling(dt);
            }
            else
            {
                UpdateClimb(dt);
                UpdateTorsoAndArms();
            }

            // 放大镜对准"角色当前攀爬手"的真实世界位置（在手臂被 IK 摆放之后读取）
            if (_showMagnifier)
            {
                Vector3 handWorld = _character.GetHandPosition(_currentHand);
                _magnifier.UpdateLens(handWorld, _magnifierScreen, _haptics.CurrentTier);
            }
            else
            {
                _magnifier.Hide();
            }

            if (_staminaBar != null) _staminaBar.SetRatio(_stamina.Ratio);
            if (_zoneOverlay != null)
            {
                _zoneOverlay.SetActiveSide(_currentHand);
                _zoneOverlay.SetTouchMarker(_lastTouchScreen, _showTouchMarker);
            }
        }

        private void UpdateClimb(float dt)
        {
            // 起攀首手 / 换手后等待对应侧按下
            if (_state == ClimbState.WaitingForPress)
            {
                bool stable = true;
                if (_currentHand == ClimbHand.None)
                {
                    if (_input.TryGetAnyNewPress(out ClimbPointer p, out ClimbHand side))
                    {
                        BeginReach(side, p);
                        stable = false;
                    }
                }
                else
                {
                    if (_input.TryGetNewPress(_currentHand, out ClimbPointer p))
                    {
                        BeginReach(_currentHand, p);
                        stable = false;
                    }
                }

                if (stable && _state == ClimbState.WaitingForPress)
                {
                    _stamina.Recover(dt);
                }
            }

            if (_state == ClimbState.Reaching)
            {
                if (_input.TryGetPointerById(_trackedFinger, out ClimbPointer p) && p.Phase != ClimbPointerPhase.Ended)
                {
                    Vector3 target = _projector.Project(p.ScreenPos, out _);
                    _lastTouchScreen = p.ScreenPos;
                    _showTouchMarker = true;
                    _attackHandCurrent = SmoothTo(_attackHandCurrent, target, _tuning.handFollowLerp);

                    RivetPoint nearest = _rivets.FindNearestExcluding(_attackHandCurrent, _excludeRivet, out float dist);
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
            else if (_state == ClimbState.Returning)
            {
                Vector3 anchor = AnchorOf(_currentHand);
                _attackHandCurrent = SmoothTo(_attackHandCurrent, anchor, _tuning.handReturnLerp);
                _stamina.Drain(dt, _staminaCfg.abandonDrainMultiplier);
                _magnifier.Hide();
                _haptics.ClearProximity();

                if ((_attackHandCurrent - anchor).sqrMagnitude < 0.0025f)
                {
                    _state = ClimbState.WaitingForPress;
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
            _torsoCenter = SmoothTo(_torsoCenter, mid, _tuning.torsoFollowLerp);

            _character.SetTorsoCenter(_torsoCenter);
            _character.DriveArm(ClimbHand.Left, leftGoal);
            _character.DriveArm(ClimbHand.Right, rightGoal);
        }

        private Vector3 GoalFor(ClimbHand hand)
        {
            bool attacking = hand == _currentHand &&
                             (_state == ClimbState.Reaching || _state == ClimbState.Returning);
            return attacking ? _attackHandCurrent : AnchorOf(hand);
        }

        private void BeginReach(ClimbHand side, ClimbPointer pointer)
        {
            _currentHand = side;
            _trackedFinger = pointer.Id;
            _attackHandCurrent = AnchorOf(side);
            // 本次伸手排除该手正抓着的那颗铆钉，必须去抓一颗新的才算完成
            _excludeRivet = side == ClimbHand.Left ? _leftRivet : _rightRivet;
            _state = ClimbState.Reaching;
        }

        private void BeginReturn()
        {
            _magnifier.Hide();
            _haptics.ClearProximity();
            _state = ClimbState.Returning;
        }

        private void Grab(RivetPoint rivet)
        {
            SetAnchor(_currentHand, rivet.GrabPosition);
            if (_currentHand == ClimbHand.Left) _leftRivet = rivet;
            else _rightRivet = rivet;
            _attackHandCurrent = rivet.GrabPosition;
            _haptics.GrabPulse();
            _haptics.ClearProximity();
            _magnifier.Hide();
            _excludeRivet = null;
            _currentHand = _currentHand.Other();
            _state = ClimbState.WaitingForPress;
        }

        private void BeginFall()
        {
            _magnifier.Hide();
            _haptics.ClearProximity();
            _state = ClimbState.Falling;
            _fallStartY = _character.TorsoWorldPosition.y;
            _fallTimer = 0f;

            float down = _fallConfig != null ? _fallConfig.initialDownSpeed : 1.5f;
            float push = _fallConfig != null ? _fallConfig.pushFromWall : 1.2f;
            // 向下 + 离墙（-Z 朝相机方向）
            Vector3 vel = Vector3.down * down + Vector3.back * push;
            _character.EnterRagdoll(vel);

            if (_camera != null) _camera.SetFalling(true);
        }

        private void UpdateFalling(float dt)
        {
            _fallTimer += dt;
            float torsoY = _character.TorsoWorldPosition.y;
            float dropped = _fallStartY - torsoY;

            float fallDistance = _staminaCfg != null ? _staminaCfg.fallDistance : 2.2f;
            float maxSeconds = _staminaCfg != null ? _staminaCfg.maxFallSeconds : 1.5f;

            if (dropped >= fallDistance || _fallTimer >= maxSeconds)
            {
                Land(torsoY);
            }
        }

        private void Land(float torsoY)
        {
            _character.ExitRagdoll();

            Vector3 landCenter = new Vector3(_torsoCenter.x, torsoY, _climbZ);
            bool snap = _fallConfig == null || _fallConfig.snapToNearestRivetOnLand;
            if (snap)
            {
                RivetPoint below = _rivets.FindNearestBelow(
                    new Vector3(_torsoCenter.x, torsoY, _climbZ), _fallStartY, out _);
                if (below != null)
                {
                    landCenter = new Vector3(below.GrabPosition.x, below.GrabPosition.y + 0.1f, _climbZ);
                }
            }

            _torsoCenter = landCenter;
            _leftAnchor = ComputeRestAnchor(landCenter, ClimbHand.Left);
            _rightAnchor = ComputeRestAnchor(landCenter, ClimbHand.Right);
            _attackHandCurrent = _leftAnchor;

            _character.SetTorsoCenter(landCenter);
            _character.DriveArm(ClimbHand.Left, _leftAnchor);
            _character.DriveArm(ClimbHand.Right, _rightAnchor);

            _stamina.ResetToRatio(_staminaCfg != null ? _staminaCfg.recoverOnLandRatio : 0.6f);
            // 摔落后双手回到默认位（不再抓在任何铆钉上）
            _leftRivet = null;
            _rightRivet = null;
            _excludeRivet = null;
            _currentHand = ClimbHand.None;
            _state = ClimbState.WaitingForPress;
            if (_camera != null) _camera.SetFalling(false);
        }

        private Vector3 ComputeRestAnchor(Vector3 center, ClimbHand hand)
        {
            float sign = hand == ClimbHand.Left ? -1f : 1f;
            Vector3 shoulder = center + new Vector3(sign * _rig.shoulderHalfWidth, _rig.shoulderVerticalOffset, 0f);
            Vector3 off = _rig.restHandOffset;
            return shoulder + new Vector3(off.x * sign, off.y, off.z);
        }

        private Vector3 AnchorOf(ClimbHand hand) => hand == ClimbHand.Left ? _leftAnchor : _rightAnchor;

        private void SetAnchor(ClimbHand hand, Vector3 pos)
        {
            if (hand == ClimbHand.Left) _leftAnchor = pos;
            else _rightAnchor = pos;
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
