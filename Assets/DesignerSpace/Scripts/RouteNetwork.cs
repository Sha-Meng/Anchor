using System.Collections.Generic;
using UnityEngine;

namespace DesignerSpace
{
    /// <summary>
    /// 动线网络运行时脚本。
    ///
    /// 挂在场景里的 "RouteNetwork" 根对象上（其下有 RouteNodes 与 ScatterAnchors 容器）。
    /// 初始化时读取 <see cref="LevelMgr"/> 持有的 <see cref="LevelGlobalConfig"/>：
    /// 若为 Debug 模式，则给 ScatterAnchors 下的每个 <see cref="AnchorPoint"/> 挂上
    /// <see cref="AnchorRadiusVisualizer"/>，在 Game 视图显示其位置与核心/最大半径。
    /// Release 模式下显示起始锚点中心，并在后续高度零星显示更淡的路线引导。
    /// </summary>
    [DisallowMultipleComponent]
    public class RouteNetwork : MonoBehaviour
    {
        [Tooltip("撒点容器子对象名，默认 ScatterAnchors")]
        [SerializeField] private string scatterContainerName = "ScatterAnchors";

        [Tooltip("可选：显式指定关卡管理器；留空则使用 LevelMgr.Instance")]
        [SerializeField] private LevelMgr levelMgr;

        [Tooltip("Release 模式下暴露前几个撒点作为路线引导")]
        [Min(0)]
        [SerializeField] private int releaseGuideAnchorCount = 20;

        [Tooltip("Release 模式下，起始引导点之后的低处撒点显示比例")]
        [Range(0f, 1f)]
        [SerializeField] private float releaseSparseGuideChanceNearBottom = 0.45f;

        [Tooltip("Release 模式下，起始引导点之后的高处撒点显示比例")]
        [Range(0f, 1f)]
        [SerializeField] private float releaseSparseGuideChanceNearTop = 0.08f;

        [Tooltip("Release 模式下，起始引导点之后的低处撒点透明度倍率")]
        [Range(0f, 1f)]
        [SerializeField] private float releaseSparseGuideAlphaNearBottom = 0.6f;

        [Tooltip("Release 模式下，起始引导点之后的高处撒点透明度倍率")]
        [Range(0f, 1f)]
        [SerializeField] private float releaseSparseGuideAlphaNearTop = 0.18f;

        private readonly List<AnchorRadiusVisualizer> _visualizers = new List<AnchorRadiusVisualizer>();

        private void Start()
        {
            LevelMgr mgr = levelMgr != null ? levelMgr : LevelMgr.Instance;
            LevelGlobalConfig config = mgr != null ? mgr.Config : null;

            if (config == null)
            {
                Debug.LogWarning("[RouteNetwork] 未找到 LevelMgr 或其 LevelGlobalConfig，跳过调试可视化。", this);
                return;
            }

            if (config.IsDebug)
            {
                EnableScatterDebugView(config);
                return;
            }

            EnableReleaseGuideView(config);
        }

        /// <summary>
        /// 给撒点容器下的所有 AnchorPoint 挂载半径可视化组件。
        /// </summary>
        private void EnableScatterDebugView(LevelGlobalConfig config)
        {
            AnchorPoint[] anchors = CollectScatterAnchors();
            if (anchors.Length == 0)
            {
                Debug.LogWarning($"[RouteNetwork] 未在 '{scatterContainerName}' 下找到任何 AnchorPoint。", this);
                return;
            }

            _visualizers.Clear();
            foreach (AnchorPoint anchor in anchors)
            {
                if (anchor == null)
                {
                    continue;
                }

                var visualizer = anchor.GetComponent<AnchorRadiusVisualizer>();
                if (visualizer == null)
                {
                    visualizer = anchor.gameObject.AddComponent<AnchorRadiusVisualizer>();
                }

                visualizer.Configure(
                    config.coreRadiusColor,
                    config.maxRadiusColor,
                    config.centerColor,
                    config.circleSegments,
                    config.ringLineWidth);

                _visualizers.Add(visualizer);
            }
        }

        /// <summary>
        /// Release 模式暴露起始若干撒点中心，并在后续高度零星保留更淡的中心点引导，不显示半径信息。
        /// </summary>
        private void EnableReleaseGuideView(LevelGlobalConfig config)
        {
            if (releaseGuideAnchorCount <= 0)
            {
                return;
            }

            AnchorPoint[] anchors = CollectScatterAnchors();
            if (anchors.Length == 0)
            {
                Debug.LogWarning($"[RouteNetwork] 未在 '{scatterContainerName}' 下找到任何 AnchorPoint，无法生成发布模式引导点。", this);
                return;
            }

            _visualizers.Clear();
            int visibleCount = 0;
            Bounds heightBounds = CalculateAnchorHeightBounds(anchors);
            for (int i = 0; i < anchors.Length; i++)
            {
                AnchorPoint anchor = anchors[i];
                if (anchor == null)
                {
                    continue;
                }

                bool isStartingGuide = visibleCount < releaseGuideAnchorCount;
                float height01 = Mathf.InverseLerp(heightBounds.min.y, heightBounds.max.y, anchor.transform.position.y);
                if (!isStartingGuide && !ShouldShowSparseReleaseGuide(i, height01))
                {
                    continue;
                }

                var visualizer = anchor.GetComponent<AnchorRadiusVisualizer>();
                if (visualizer == null)
                {
                    visualizer = anchor.gameObject.AddComponent<AnchorRadiusVisualizer>();
                }

                Color centerColor = isStartingGuide
                    ? config.centerColor
                    : ReleaseSparseGuideColor(config.centerColor, height01);

                visualizer.ConfigureCenterOnly(
                    centerColor,
                    config.circleSegments,
                    config.ringLineWidth);

                _visualizers.Add(visualizer);
                visibleCount++;
            }
        }

        private bool ShouldShowSparseReleaseGuide(int anchorIndex, float height01)
        {
            float chance = Mathf.Lerp(
                releaseSparseGuideChanceNearBottom,
                releaseSparseGuideChanceNearTop,
                Mathf.Clamp01(height01));
            return Deterministic01(anchorIndex) <= chance;
        }

        private Color ReleaseSparseGuideColor(Color baseColor, float height01)
        {
            float alphaMultiplier = Mathf.Lerp(
                releaseSparseGuideAlphaNearBottom,
                releaseSparseGuideAlphaNearTop,
                Mathf.Clamp01(height01));
            baseColor.a *= alphaMultiplier;
            return baseColor;
        }

        private static float Deterministic01(int value)
        {
            unchecked
            {
                uint hash = (uint)value + 0x9E3779B9u;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                hash ^= hash >> 16;
                return (hash & 0x00FFFFFFu) / 16777215f;
            }
        }

        private static Bounds CalculateAnchorHeightBounds(AnchorPoint[] anchors)
        {
            bool hasAny = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (AnchorPoint anchor in anchors)
            {
                if (anchor == null)
                {
                    continue;
                }

                if (!hasAny)
                {
                    bounds = new Bounds(anchor.transform.position, Vector3.zero);
                    hasAny = true;
                    continue;
                }

                bounds.Encapsulate(anchor.transform.position);
            }

            return bounds;
        }

        private AnchorPoint[] CollectScatterAnchors()
        {
            Transform container = transform.Find(scatterContainerName);
            if (container != null)
            {
                return container.GetComponentsInChildren<AnchorPoint>(true);
            }

            // 未找到指定容器时，退化为扫描整个动线网络下的 AnchorPoint。
            return GetComponentsInChildren<AnchorPoint>(true);
        }
    }
}
