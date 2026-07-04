using UnityEngine;

namespace ClimbGame.Character
{
    /// <summary>
    /// Drives a flipbook sprite animation from an <see cref="IClimberMotion"/> source.
    /// Plays the climb cycle while moving (paced by climb speed), shows the idle pose
    /// while still, and flips horizontally to match facing. It is a pure consumer of
    /// motion state and sprites, with no knowledge of input or movement maths.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [AddComponentMenu("ClimbGame/Climber Animator")]
    public sealed class ClimberAnimator : MonoBehaviour
    {
        [Tooltip("Animation frames per second at full climb speed.")]
        [SerializeField] private float framesPerSecond = 10f;
        [Tooltip("Lowest playback rate (as a fraction of full) while climbing slowly.")]
        [Range(0f, 1f)]
        [SerializeField] private float minPlaybackRate = 0.25f;

        private SpriteRenderer _renderer;
        private IClimberMotion _motion;
        private Sprite[] _climbFrames;
        private Sprite _idle;
        private int _frameIndex;
        private float _frameTimer;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void Bind(IClimberMotion motion) => _motion = motion;

        public void SetSprites(Sprite[] climbFrames, Sprite idle)
        {
            _climbFrames = climbFrames;
            _idle = idle != null ? idle
                : (climbFrames != null && climbFrames.Length > 0 ? climbFrames[0] : null);

            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer != null && _idle != null) _renderer.sprite = _idle;
        }

        private void Update()
        {
            if (_renderer == null || _motion == null) return;

            if (_motion.Facing != 0)
                _renderer.flipX = _motion.Facing < 0;

            bool canAnimate = _climbFrames != null && _climbFrames.Length > 0;
            if (_motion.State == ClimbState.Climbing && canAnimate)
            {
                float rate = Mathf.Max(minPlaybackRate, _motion.ClimbSpeed01);
                _frameTimer += Time.deltaTime * framesPerSecond * rate;
                while (_frameTimer >= 1f)
                {
                    _frameTimer -= 1f;
                    _frameIndex = (_frameIndex + 1) % _climbFrames.Length;
                }
                _renderer.sprite = _climbFrames[_frameIndex];
            }
            else
            {
                _frameTimer = 0f;
                _frameIndex = 0;
                if (_idle != null) _renderer.sprite = _idle;
            }
        }
    }
}
