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

        private static LevelMgr _instance;

        /// <summary>
        /// 关卡管理器全局单例。
        ///
        /// 首次访问时若尚未注册，会自动在场景中查找已存在的 <see cref="LevelMgr"/>；
        /// 找不到则返回 null，由调用方自行处理（本项目不自动创建，避免污染场景）。
        /// </summary>
        public static LevelMgr Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<LevelMgr>();
                }

                return _instance;
            }
            private set => _instance = value;
        }

        /// <summary>关卡全局配置，可能为空（未在 Inspector 中指定）。</summary>
        public LevelGlobalConfig Config => config;

        /// <summary>是否处于 Debug 模式（配置缺失时按 Release 处理）。</summary>
        public bool IsDebug => config != null && config.IsDebug;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
