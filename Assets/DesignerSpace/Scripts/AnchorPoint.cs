using UnityEngine;

namespace DesignerSpace
{
    /// <summary>
    /// 场景中离散分布的锚点标记。
    /// 两档半径既用于编辑器 Gizmo 预览，也是运行时抓握区域判定的事实来源：
    /// <see cref="CoreRadius"/> 为核心区域上界，<see cref="OuterRadius"/> 为外环区域上界，
    /// 供 MouseFollowJitter 分档以及 HandFollowController 的 hook 松手区域判定共用。
    /// </summary>
    public class AnchorPoint : MonoBehaviour
    {
        [Tooltip("抓点自身质量，范围 1-10。距离衰减后的查询稳定值不会超过该值。")]
        [Range(1, 10)]
        public int baseStability = 10;

        [Tooltip("核心区域半径：距离 <= 该值视为核心区域（编辑器预览 + 运行时区域判定共用）")]
        public float previewIntenseRadius = 1f;

        [Tooltip("外环区域半径：核心半径 < 距离 <= 该值视为外环区域，超出则脱离（编辑器预览 + 运行时区域判定共用）")]
        public float previewSlightRadius = 2.5f;

        public int BaseStability => Mathf.Clamp(baseStability, 1, 10);

        /// <summary>核心区域半径（运行时判定用），等价于 <see cref="previewIntenseRadius"/>。</summary>
        public float CoreRadius => Mathf.Max(0f, previewIntenseRadius);

        /// <summary>外环区域半径（运行时判定用），等价于 <see cref="previewSlightRadius"/>。</summary>
        public float OuterRadius => Mathf.Max(0f, previewSlightRadius);

        public float GrabRadius => Mathf.Max(0f, previewSlightRadius);

        private void OnValidate()
        {
            baseStability = Mathf.Clamp(baseStability, 1, 10);
            previewIntenseRadius = Mathf.Max(0f, previewIntenseRadius);
            previewSlightRadius = Mathf.Max(0f, previewSlightRadius);
        }

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
