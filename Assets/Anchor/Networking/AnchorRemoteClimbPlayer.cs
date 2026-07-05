using ClimbGame.Climb3C.Character;
using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using ClimbGame.Climb3C.Gameplay;
using UnityEngine;

namespace Anchor.Networking
{
    public sealed class AnchorRemoteClimbPlayer
    {
        private readonly ClimbCharacter _avatar;
        private readonly Transform _root;
        private Vector3 _targetTorso;
        private Vector3 _targetLeftHand;
        private Vector3 _targetRightHand;
        private bool _hasTarget;
        private int _lastSeq = -1;

        public Transform Root => _root;
        public int LastSeq => _lastSeq;
        public bool IsPeerLeft { get; private set; }
        public float Health { get; private set; } = 100f;
        public float MaxHealth { get; private set; } = 100f;
        public bool IsFailed { get; private set; }

        public AnchorRemoteClimbPlayer(string name, Vector3 torso, Vector3 leftHand, Vector3 rightHand, Color bodyColor, Color handColor)
        {
            var armRig = ScriptableObject.CreateInstance<ArmRigConfig>();
            var fall = ScriptableObject.CreateInstance<RagdollFallConfig>();
            var shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            var bodyMat = new Material(shader) { color = bodyColor };
            var handMat = new Material(shader) { color = handColor };

            _avatar = new ClimbCharacter(armRig, fall);
            _avatar.Build(null, torso, bodyMat, handMat);
            _root = _avatar.Root;
            _root.name = name;

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

            var current = _avatar.TorsoCenter;
            var t = 1f - Mathf.Exp(-12f * deltaTime);
            var torso = Vector3.Lerp(current, _targetTorso, t);
            var left = Vector3.Lerp(_avatar.GetHandPosition(ClimbHand.Left), _targetLeftHand, t);
            var right = Vector3.Lerp(_avatar.GetHandPosition(ClimbHand.Right), _targetRightHand, t);
            ApplyTargets(torso, left, right, false);
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

        private void SetTint(Color color)
        {
            if (_root == null) return;

            var renderers = _root.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].material.color = color;
            }
        }
    }
}
