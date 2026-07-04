using System.Text;
using Anchor.ForceSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.SystemValidation
{
    [DisallowMultipleComponent]
    public sealed class SystemValidationDebugPanel : MonoBehaviour
    {
        [SerializeField] private Text outputText;
        [SerializeField] private float refreshIntervalSeconds = 0.05f;

        private readonly StringBuilder _builder = new StringBuilder(1024);
        private float _nextRefreshTime;

        public void UpdateFrom(SystemValidationController controller)
        {
            if (outputText == null || controller == null || Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.unscaledTime + Mathf.Max(0f, refreshIntervalSeconds);
            _builder.Length = 0;
            _builder.AppendLine("System Validation Test");
            _builder.Append("Anchors: ").Append(controller.RegisteredAnchorCount)
                .Append(" registered, ").Append(controller.SkippedAnchorCount).AppendLine(" skipped");
            AppendHand("Left", controller.LeftHand, controller.LastResult.LeftHand);
            AppendHand("Right", controller.RightHand, controller.LastResult.RightHand);
            _builder.Append("State: ").Append(controller.LastResult.State)
                .Append("  Fall: ").Append(controller.LastResult.FallTriggered).AppendLine();
            _builder.Append("Reason: ").Append(controller.LastResult.PrimaryFailureReason)
                .Append("  Changed: ").Append(controller.LastStateChangeTime.ToString("0.00")).AppendLine();
            _builder.Append("Thresholds: min=")
                .Append(controller.LastSettings.MinGripQuality.ToString("0.00"))
                .Append(" stable=")
                .Append(controller.LastSettings.StableGripQuality.ToString("0.00"))
                .Append(" fallDelay=")
                .Append(controller.LastSettings.BothHandsFallDelaySeconds.ToString("0.00"))
                .AppendLine();
            outputText.text = _builder.ToString();
        }

        private void AppendHand(string label, ValidationHandInputState hand, ForceHandEvaluation evaluation)
        {
            _builder.AppendLine();
            _builder.Append(label).Append(": touching=").Append(hand.IsTouching)
                .Append(" effective=").Append(evaluation.IsEffective)
                .Append(" quality=").Append(evaluation.GripQuality.ToString("0.00"))
                .Append(" failure=").Append(evaluation.FailureReason).AppendLine();
            _builder.Append("  world=").Append(FormatVector(hand.WorldPosition))
                .Append(" screen=").Append(hand.ScreenPosition.ToString("0")).AppendLine();
            _builder.Append("  anchor=").Append(hand.NearestAnchor.Found ? hand.NearestAnchor.DebugName : "None")
                .Append(" id=").Append(hand.NearestAnchor.Id)
                .Append(" dist=").Append(FormatFloat(hand.NearestAnchor.Distance))
                .Append(" stability=").Append(hand.NearestAnchor.CurrentStability).AppendLine();
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:0.00}, {value.y:0.00}, {value.z:0.00})";
        }

        private static string FormatFloat(float value)
        {
            return float.IsInfinity(value) ? "inf" : value.ToString("0.00");
        }
    }
}
