using UnityEngine;

namespace DesignerSpace
{
    /// <summary>
    /// 预研：场景中离散分布的锚点标记。
    /// 本身不含任何运行时逻辑，仅作为 MouseFollowJitter 检索的目标，
    /// 并在编辑器中绘制两档阈值半径方便可视化校验。
    /// </summary>
    public class AnchorPoint : MonoBehaviour
    {
        [Tooltip("仅用于编辑器 Gizmo 预览，应与 MouseFollowJitter 的 intenseRadius 保持一致")]
        public float previewIntenseRadius = 1f;

        [Tooltip("仅用于编辑器 Gizmo 预览，应与 MouseFollowJitter 的 slightRadius 保持一致")]
        public float previewSlightRadius = 2.5f;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, previewIntenseRadius);

            Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, previewSlightRadius);

            Gizmos.color = Color.white;
            Gizmos.DrawSphere(transform.position, 0.15f);
        }
    }
}
