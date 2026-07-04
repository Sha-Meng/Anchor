using UnityEngine;

namespace ClimbGame.Inputs
{
    /// <summary>
    /// WASD / arrow-key climb input for convenient in-Editor debugging.
    /// Reads explicit keys (not the Input Manager axes) so it works regardless of
    /// project axis configuration.
    /// </summary>
    [AddComponentMenu("ClimbGame/Input/Keyboard Climb Input")]
    public sealed class KeyboardClimbInput : MonoBehaviour, IClimbInput
    {
        public Vector2 Direction
        {
            get
            {
                float x = 0f;
                float y = 0f;

                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;

                var v = new Vector2(x, y);
                // Keep diagonals from being faster than the cardinal directions.
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }
    }
}
