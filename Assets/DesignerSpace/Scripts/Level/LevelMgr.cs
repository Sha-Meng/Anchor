using UnityEngine;

namespace DesignerSpace
{
    /// <summary>
    /// 关卡管理器。
    ///
    /// 挂在场景里的常驻对象上，持有 <see cref="LevelGlobalConfig"/> 作为整关的全局配置来源，
    /// 并通过 <see cref="Instance"/> 让其他系统（如 <see cref="RouteNetwork"/>）在运行时读取运行模式。
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelMgr : MonoBehaviour
    {
        [Tooltip("关卡全局配置资源（勾选 Debug/Release 模式）")]
        [SerializeField] private LevelGlobalConfig config;

        /// <summary>当前场景中的关卡管理器实例。</summary>
        public static LevelMgr Instance { get; private set; }

        /// <summary>关卡全局配置，可能为空（未在 Inspector 中指定）。</summary>
        public LevelGlobalConfig Config => config;

        /// <summary>是否处于 Debug 模式（配置缺失时按 Release 处理）。</summary>
        public bool IsDebug => config != null && config.IsDebug;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
