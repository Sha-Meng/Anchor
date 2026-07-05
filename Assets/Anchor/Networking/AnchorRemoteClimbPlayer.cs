using ClimbGame.Climb3C.Character;
using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using ClimbGame.Climb3C.Gameplay;
using FIMSpace.FProceduralAnimation;
using UnityEngine;

namespace Anchor.Networking
{
    public sealed class AnchorRemoteClimbPlayer
    {
        private readonly IClimberAvatar _avatar;
        private readonly MagnetClimberAvatar _magnetAvatar;
        private readonly Transform _root;
        private Vector3 _targetTorso;
        private Vector3 _targetLeftHand;
        private Vector3 _targetRightHand;
        private bool _hasTarget;
        private int _lastSeq = -1;
        private int _pendingRagdollWarpFrames;

        public Transform Root => _root;
        public int LastSeq => _lastSeq;
        public bool IsPeerLeft { get; private set; }
        public float Health { get; private set; } = 100f;
        public float MaxHealth { get; private set; } = 100f;
        public bool IsFailed { get; private set; }

        public AnchorRemoteClimbPlayer(string name, Vector3 torso, Vector3 leftHand, Vector3 rightHand, Color bodyColor, Color handColor)
            : this(name, torso, leftHand, rightHand, bodyColor, handColor, null, Vector3.zero, 1f, Vector3.zero, 1.6f, 0.3f)
        {
        }

        public AnchorRemoteClimbPlayer(
            string name,
            Vector3 torso,
            Vector3 leftHand,
            Vector3 rightHand,
            Color bodyColor,
            Color handColor,
            GameObject characterSource,
            Vector3 initialEuler,
            float characterScale,
            Vector3 capsuleCenter,
            float capsuleHeight,
            float capsuleRadius)
        {
            var armRig = ScriptableObject.CreateInstance<ArmRigConfig>();
            var fall = ScriptableObject.CreateInstance<RagdollFallConfig>();

            Transform avatarRoot;
            if (characterSource != null)
            {
                var remoteCharacter = Object.Instantiate(characterSource);
                remoteCharacter.name = name + " Character";
                var avatar = new MagnetClimberAvatar(
                    remoteCharacter,
                    armRig,
                    fall,
                    initialEuler,
                    characterScale,
                    MakeRemoteMagnetPrefix(name),
                    false);
                avatar.Build(null, torso, null, null);
                DisableRemoteBehaviours(avatar.Root);
                avatar.SetInitialTransform(torso);
                avatar.CommitClimbRagdoll(leftHand, rightHand);
                _avatar = avatar;
                _magnetAvatar = avatar;
                avatarRoot = avatar.Root;
                _pendingRagdollWarpFrames = 3;
            }
            else
            {
                var shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
                var bodyMat = new Material(shader) { color = bodyColor };
                var handMat = new Material(shader) { color = handColor };
                var avatar = new ClimbCharacter(armRig, fall);
                avatar.Build(null, torso, bodyMat, handMat);
                _avatar = avatar;
                avatarRoot = avatar.Root;
            }

            _root = avatarRoot;
            if (_root != null) _root.name = name;
            SetTint(bodyColor, handColor);

            ApplyTargets(torso, leftHand, rightHand, true);
        }

        public bool TryApplyState(int seq, Vector3 torso, Vector3 leftHand, Vector3 rightHand, float health, float maxHealth, bool isFailed)
        {
            if (seq <= _lastSeq) return false;

            _lastSeq = seq;
            _targetTorso = torso;
            _targetLeftHand = leftHand;
            _targetRightHand = rightHand;
            MaxHealth = Mathf.Max(1f, maxHealth);
            Health = Mathf.Clamp(health, 0f, MaxHealth);
            IsFailed = isFailed;
            _hasTarget = true;
            IsPeerLeft = false;
            if (isFailed)
            {
                SetTint(new Color(0.45f, 0.1f, 0.1f, 0.85f));
            }
            return true;
        }

        public void MarkFailed()
        {
            IsFailed = true;
            Health = 0f;
            SetTint(new Color(0.45f, 0.1f, 0.1f, 0.85f));
        }

        public void Update(float deltaTime)
        {
            if (!_hasTarget || IsPeerLeft) return;

            var current = _avatar.TorsoWorldPosition;
            var t = 1f - Mathf.Exp(-12f * deltaTime);
            var torso = Vector3.Lerp(current, _targetTorso, t);
            var left = Vector3.Lerp(_avatar.GetHandPosition(ClimbHand.Left), _targetLeftHand, t);
            var right = Vector3.Lerp(_avatar.GetHandPosition(ClimbHand.Right), _targetRightHand, t);
            ApplyTargets(torso, left, right, false);

            if (_pendingRagdollWarpFrames > 0)
            {
                _pendingRagdollWarpFrames--;
                if (_pendingRagdollWarpFrames == 0)
                {
                    _magnetAvatar?.SetRagdollPosition(torso);
                }
            }
        }

        public void MarkPeerLeft()
        {
            IsPeerLeft = true;
            SetTint(new Color(0.35f, 0.35f, 0.35f, 0.75f));
        }

        private void ApplyTargets(Vector3 torso, Vector3 leftHand, Vector3 rightHand, bool resetPose)
        {
            if (resetPose)
            {
                _avatar.SetupClimbPose(torso);
            }

            _avatar.SetTorsoCenter(torso);
            _avatar.DriveArm(ClimbHand.Left, leftHand, true);
            _avatar.DriveArm(ClimbHand.Right, rightHand, true);
        }

        private static void DisableRemoteBehaviours(Transform root)
        {
            if (root == null) return;

            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null && !(behaviours[i] is RagdollAnimator2))
                {
                    behaviours[i].enabled = false;
                }
            }
        }

        private static string MakeRemoteMagnetPrefix(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Remote_";

            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars) + "_";
        }

        private void SetTint(Color bodyColor, Color handColor)
        {
            if (_root == null) return;

            var renderers = _root.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].material.color = IsUnderHand(renderers[i].transform) ? handColor : bodyColor;
            }
        }

        private void SetTint(Color color)
        {
            SetTint(color, color);
        }

        private bool IsUnderHand(Transform transform)
        {
            while (transform != null && transform != _root)
            {
                if (transform.name.IndexOf("Hand", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }

                transform = transform.parent;
            }

            return false;
        }
    }
}
