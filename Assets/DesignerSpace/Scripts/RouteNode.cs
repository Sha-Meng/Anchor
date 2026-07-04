using System.Collections.Generic;
using UnityEngine;

namespace DesignerSpace
{
    /// <summary>
    /// 关卡动线上的一个节点。
    ///
    /// 每个节点是墙面上被"打"出来的点，通过 <see cref="nextNodes"/> 记录它指向的后继节点，
    /// 从而表达先后关系（先打的点指向后打的点）。一个节点可以有多个后继，构成自由有向图。
    /// 节点自身在编辑器中用 Gizmos 绘制指向每个后继的虚线箭头，方便策划直接在 Scene 视图查看动线。
    /// </summary>
    [DisallowMultipleComponent]
    public class RouteNode : MonoBehaviour
    {
        [Tooltip("本节点指向的后继节点列表（有向边：this -> next）")]
        public List<RouteNode> nextNodes = new List<RouteNode>();

        [Tooltip("可选的节点标签，便于策划标注含义")]
        public string label = string.Empty;

        [Header("Gizmo 外观")]
        [Tooltip("节点球体颜色")]
        public Color nodeColor = new Color(0.2f, 0.85f, 1f, 1f);

        [Tooltip("有向边虚线颜色")]
        public Color edgeColor = new Color(1f, 0.85f, 0.2f, 1f);

        [Tooltip("节点球体半径")]
        public float nodeRadius = 0.12f;

        private const float DashLength = 0.18f;
        private const float GapLength = 0.12f;
        private const float ArrowHeadLength = 0.28f;
        private const float ArrowHeadWidth = 0.14f;

        /// <summary>
        /// 尝试添加一条从本节点指向 target 的有向边。
        /// 会过滤自环与重复边，成功添加返回 true。
        /// </summary>
        public bool TryAddNext(RouteNode target)
        {
            if (target == null || target == this)
            {
                return false;
            }

            if (nextNodes == null)
            {
                nextNodes = new List<RouteNode>();
            }

            if (nextNodes.Contains(target))
            {
                return false;
            }

            nextNodes.Add(target);
            return true;
        }

        private void OnDrawGizmos()
        {
            DrawNodeGizmo(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawNodeGizmo(true);
        }

        private void DrawNodeGizmo(bool selected)
        {
            Gizmos.color = nodeColor;
            Gizmos.DrawSphere(transform.position, nodeRadius);

            if (nextNodes == null)
            {
                return;
            }

            Gizmos.color = edgeColor;
            for (int i = 0; i < nextNodes.Count; i++)
            {
                RouteNode next = nextNodes[i];
                if (next == null)
                {
                    continue;
                }

                DrawDashedArrow(transform.position, next.transform.position, next.nodeRadius);
            }
        }

        /// <summary>
        /// 从 from 到 to 绘制一条虚线 + 末端箭头。箭头在 to 附近、指向 to。
        /// </summary>
        private static void DrawDashedArrow(Vector3 from, Vector3 to, float targetRadius)
        {
            Vector3 delta = to - from;
            float totalLength = delta.magnitude;
            if (totalLength < Mathf.Epsilon)
            {
                return;
            }

            Vector3 dir = delta / totalLength;

            // 让线段止于目标球体表面，避免箭头戳进节点球里。
            float usableLength = Mathf.Max(0f, totalLength - Mathf.Max(targetRadius, 0f));
            Vector3 tip = from + dir * usableLength;

            DrawDashedLine(from, tip);
            DrawArrowHead(tip, dir);
        }

        private static void DrawDashedLine(Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            float length = delta.magnitude;
            if (length < Mathf.Epsilon)
            {
                return;
            }

            Vector3 dir = delta / length;
            float step = DashLength + GapLength;
            float traveled = 0f;

            while (traveled < length)
            {
                float dashStart = traveled;
                float dashEnd = Mathf.Min(traveled + DashLength, length);
                Gizmos.DrawLine(start + dir * dashStart, start + dir * dashEnd);
                traveled += step;
            }
        }

        private static void DrawArrowHead(Vector3 tip, Vector3 dir)
        {
            // 选一个与 dir 不平行的参考轴，构造出箭头张开所在平面。
            Vector3 reference = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
            Vector3 side = Vector3.Cross(dir, reference).normalized;

            Vector3 basePoint = tip - dir * ArrowHeadLength;
            Vector3 left = basePoint + side * ArrowHeadWidth;
            Vector3 right = basePoint - side * ArrowHeadWidth;

            Gizmos.DrawLine(tip, left);
            Gizmos.DrawLine(tip, right);
        }
    }
}
