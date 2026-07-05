using ClimbGame.Climb3C.Core;
using UnityEngine;

namespace ClimbGame.Climb3C.Gameplay
{
    public struct ClimbStateSnapshot
    {
        public int PlayerId;
        public ClimbState State;
        public ClimbHand CurrentHand;
        public Vector3 TorsoCenter;
        public Vector3 LeftHandPosition;
        public Vector3 RightHandPosition;
        public int LeftRivetId;
        public int RightRivetId;
        public float StaminaRatio;
        public bool IsFalling;
        public float Health;
        public float MaxHealth;
        public bool IsFailed;
    }

    public interface IClimbStateSource
    {
        bool TryGetSnapshot(out ClimbStateSnapshot snapshot);
    }
}
