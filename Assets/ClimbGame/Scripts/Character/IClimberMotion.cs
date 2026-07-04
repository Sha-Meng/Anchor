using UnityEngine;

namespace ClimbGame.Character
{
    public enum ClimbState
    {
        Idle,
        Climbing
    }

    /// <summary>
    /// Read-only view of the climber's motion, consumed by the animation layer.
    /// This lets the animator react to movement without knowing how movement is produced
    /// (decouples animation from the controller and from input entirely).
    /// </summary>
    public interface IClimberMotion
    {
        ClimbState State { get; }

        /// <summary>Current world-space velocity in units/second.</summary>
        Vector2 Velocity { get; }

        /// <summary>Normalised climb speed 0..1, useful for pacing the animation.</summary>
        float ClimbSpeed01 { get; }

        /// <summary>Horizontal facing: -1 left, +1 right, 0 unchanged.</summary>
        int Facing { get; }
    }
}
