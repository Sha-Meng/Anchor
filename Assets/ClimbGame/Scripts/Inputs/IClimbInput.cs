using UnityEngine;

namespace ClimbGame.Inputs
{
    /// <summary>
    /// Abstraction over "where does the player want to climb".
    /// The character controller depends only on this interface, never on a concrete
    /// device, so keyboard, on-screen joystick or any future source stay interchangeable.
    /// </summary>
    public interface IClimbInput
    {
        /// <summary>
        /// Desired climb direction. Magnitude is 0..1 (0 = no intent, 1 = full push).
        /// X: horizontal (+ right / - left). Y: vertical (+ up / - down).
        /// </summary>
        Vector2 Direction { get; }
    }
}
