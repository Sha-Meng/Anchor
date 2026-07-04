using System.Collections.Generic;
using UnityEngine;

namespace ClimbGame.Inputs
{
    /// <summary>
    /// Merges several <see cref="IClimbInput"/> sources into one.
    /// Each frame the source with the strongest intent wins, so the player can freely
    /// switch between the keyboard and the on-screen joystick without any conflict.
    /// Sources are registered by the composition root, keeping this class device-agnostic.
    /// </summary>
    [AddComponentMenu("ClimbGame/Input/Composite Climb Input")]
    public sealed class CompositeClimbInput : MonoBehaviour, IClimbInput
    {
        private readonly List<IClimbInput> _sources = new List<IClimbInput>();

        public void AddSource(IClimbInput source)
        {
            if (source != null && source != this && !_sources.Contains(source))
                _sources.Add(source);
        }

        public void RemoveSource(IClimbInput source) => _sources.Remove(source);

        public Vector2 Direction
        {
            get
            {
                Vector2 best = Vector2.zero;
                float bestMagnitude = 0f;
                for (int i = 0; i < _sources.Count; i++)
                {
                    Vector2 d = _sources[i].Direction;
                    float m = d.sqrMagnitude;
                    if (m > bestMagnitude)
                    {
                        bestMagnitude = m;
                        best = d;
                    }
                }
                return best;
            }
        }
    }
}
