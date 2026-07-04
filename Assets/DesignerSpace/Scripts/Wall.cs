using UnityEngine;

namespace DesignerSpace
{
    /// <summary>
    /// 可打点墙面的标记组件。
    ///
    /// 关卡动线编辑器只允许在挂有本组件的碰撞体上打点，
    /// 以此把动线节点约束在崖壁/墙面上，避免误打在地面、人偶、道具等其他碰撞体上。
    /// 组件本身没有任何运行时逻辑，仅作为语义标识，可按需在其上扩展墙面参数。
    /// </summary>
    [DisallowMultipleComponent]
    public class Wall : MonoBehaviour
    {
        [Tooltip("仅用于编辑器 Gizmo 提示的墙面高亮颜色")]
        public Color editorHighlightColor = new Color(0.2f, 0.6f, 1f, 0.9f);

        private void OnDrawGizmosSelected()
        {
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                return;
            }

            Gizmos.color = editorHighlightColor;
            Bounds bounds = col.bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
