using UnityEngine;

namespace ClimbGame.Climb3C.Core
{
    /// <summary>
    /// 墙面上的立体铆钉抓力点。作为有效抓握目标，供 <see cref="RivetField"/> 检索。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RivetPoint : MonoBehaviour
    {
        [Tooltip("仅用于编辑器 Gizmo 预览，应与 HapticConfig.snapRadius 保持一致")]
        public float previewSnapRadius = 0.14f;

        [Tooltip("仅用于编辑器 Gizmo 预览，应与 HapticConfig.intenseRadius 保持一致")]
        public float previewIntenseRadius = 0.35f;

        [Tooltip("仅用于编辑器 Gizmo 预览，应与 HapticConfig.slightRadius 保持一致")]
        public float previewSlightRadius = 1.1f;

        /// <summary>抓握吸附的目标世界坐标（铆钉表面中心）。</summary>
        public Vector3 GrabPosition => transform.position;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, previewSnapRadius);
            Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, previewIntenseRadius);
            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, previewSlightRadius);
        }
    }
}
