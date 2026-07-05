using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Anchor.Boot
{
    /// <summary>
    /// 主桌面开始逻辑：管理"点击开始游戏"后要打开的场景。
    /// 在编辑器里可以直接把场景资源拖拽到 <see cref="sceneToLoad"/> 上；
    /// 运行时使用序列化保存的场景名进行加载。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Anchor/Boot/Start")]
    public sealed class Start : MonoBehaviour
    {
#if UNITY_EDITOR
        [Tooltip("点击开始游戏后要打开的场景，直接把场景资源拖到这里即可。")]
        [SerializeField] private SceneAsset sceneToLoad;
#endif

        // 运行时实际使用的场景名，由编辑器下的 sceneToLoad 同步而来。
        [SerializeField, HideInInspector] private string sceneName;

        [Tooltip("开始时是否显示鼠标光标（PC 主菜单通常需要）。")]
        [SerializeField] private bool showCursorOnStart = true;

        private bool _isLoading;

        private void Awake()
        {
            if (showCursorOnStart)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        /// <summary>
        /// 供开始按钮的 OnClick 事件调用：加载配置好的目标场景。
        /// </summary>
        public void StartGame()
        {
            if (_isLoading)
            {
                return;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[Start] 未配置要打开的场景，请把目标场景拖到 Start 组件的 Scene To Load 字段。", this);
                return;
            }

            _isLoading = true;
            SceneManager.LoadScene(sceneName);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 把拖拽进来的场景资源同步成运行时可用的场景名。
            sceneName = sceneToLoad != null ? sceneToLoad.name : string.Empty;
        }
#endif
    }
}
