using UnityEngine;

namespace DesignerSpace
{
    /// <summary>
    /// 标记场景中的 <see cref="AnchorPoint"/> 为关卡结算判定点。
    /// 与 AnchorPoint 挂在同一 GameObject（通常为 ScatterAnchor_*）上。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AnchorPoint))]
    [AddComponentMenu("Anchor/Settlement Anchor")]
    public sealed class SettlementAnchor : MonoBehaviour
    {
        public AnchorPoint AnchorPoint => GetComponent<AnchorPoint>();

        public static bool IsSettlementAnchor(AnchorPoint anchor)
        {
            return anchor != null && anchor.TryGetComponent<SettlementAnchor>(out _);
        }

        public static bool IsSettlementAnchor(Component component)
        {
            return component != null && component.TryGetComponent<SettlementAnchor>(out _);
        }
    }
}
