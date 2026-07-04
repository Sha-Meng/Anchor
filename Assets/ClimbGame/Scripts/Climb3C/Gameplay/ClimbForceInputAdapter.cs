using Anchor.ForceSystem;
using ClimbGame.Climb3C.Core;
using ClimbGame.Climb3C.State;
using UnityEngine;

namespace ClimbGame.Climb3C.Gameplay
{
    /// <summary>
    /// 将攀爬运行时状态转换为受力系统输入。该类不持有 Unity 对象查询，不修改角色状态。
    /// </summary>
    public sealed class ClimbForceInputAdapter : IForceInputAdapter
    {
        private ClimberRuntimeState _state;

        public void Configure(ClimberRuntimeState state)
        {
            _state = state;
        }

        public ForceEvaluationInput BuildInput(float deltaTime)
        {
            if (_state == null)
            {
                return new ForceEvaluationInput
                {
                    Body = default,
                    PreviousState = ForceState.Stable,
                    DeltaTime = deltaTime
                };
            }

            return new ForceEvaluationInput
            {
                LeftHand = BuildHandInput(ClimbHand.Left),
                RightHand = BuildHandInput(ClimbHand.Right),
                Body = new BodyForceInput
                {
                    IsAlreadyFalling = _state.State == ClimbState.Falling,
                    IsStunned = false
                },
                PreviousState = _state.ForceMemory.PreviousState,
                DeltaTime = deltaTime
            };
        }

        private HandForceInput BuildHandInput(ClimbHand hand)
        {
            var attacking = IsAttacking(hand);
            var position = attacking ? _state.AttackHandCurrent : AnchorOf(hand);

            // 当前设计下，伸出的手未抓稳；锚定手视为稳定有效抓握。
            var grip = attacking
                ? GripQueryResult.None()
                : GripQueryResult.Candidate(ForcePointType.ValidHold, 1f);

            return HandForceInput.FromGrip(true, grip, _state.StaminaRatio, position);
        }

        private bool IsAttacking(ClimbHand hand)
        {
            return hand == _state.CurrentHand &&
                   (_state.State == ClimbState.Reaching || _state.State == ClimbState.Returning);
        }

        private Vector3 AnchorOf(ClimbHand hand)
        {
            return hand == ClimbHand.Left ? _state.LeftAnchor : _state.RightAnchor;
        }
    }
}
