using System;
using UnityEngine;

namespace Anchor.Networking
{
    public enum AnchorStartPointSlot
    {
        Guest = 0,
        Host = 1
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Anchor/Networking/Start Point")]
    public sealed class StartPoint : MonoBehaviour
    {
        [SerializeField] private AnchorStartPointSlot slot = AnchorStartPointSlot.Host;
        [SerializeField] private float gizmoRadius = 0.25f;

        public AnchorStartPointSlot Slot => slot;
        public string SlotKey => slot == AnchorStartPointSlot.Host ? "host" : "guest";

        public bool MatchesSlot(string slotName)
        {
            return string.Equals(SlotKey, slotName, StringComparison.OrdinalIgnoreCase);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = slot == AnchorStartPointSlot.Host
                ? new Color(0.2f, 0.55f, 1f, 0.85f)
                : new Color(0.2f, 0.9f, 0.65f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);
        }
    }
}
