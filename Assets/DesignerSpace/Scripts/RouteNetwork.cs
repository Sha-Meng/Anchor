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
    /// Release 模式下仍显示前若干锚点中心，作为玩家起步路线引导。
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
        /// Release 模式只暴露起始若干撒点中心作为引导，不显示半径信息。
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

                visualizer.ConfigureCenterOnly(
                    config.centerColor,
                    config.circleSegments,
                    config.ringLineWidth);

                _visualizers.Add(visualizer);
                visibleCount++;
                if (visibleCount >= releaseGuideAnchorCount)
                {
                    break;
                }
            }
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
