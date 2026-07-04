#if UNITY_EDITOR
using System.IO;
using ClimbGame.Climb3C.Boot;
using ClimbGame.Climb3C.Config;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ClimbGame.EditorTools
{
    /// <summary>
    /// 攀爬 3C（3D）演示的编辑器便捷入口：一键创建场景、一键生成默认配置资产。
    /// 运行不依赖这些菜单——把 <see cref="Climb3CBootstrap"/> 挂到空物体上 Play 即可。
    /// </summary>
    public static class Climb3CEditor
    {
        private const string SceneDir = "Assets/ClimbGame/Scenes";
        private const string ScenePath = SceneDir + "/Climb3CDemo.unity";
        private const string ConfigDir = "Assets/ClimbGame/Config";

        [MenuItem("Tools/ClimbGame/3C ▸ Create & Open Demo Scene")]
        public static void CreateDemoScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("Climb3C");
            go.AddComponent<Climb3CBootstrap>();

            Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "ClimbGame 3C",
                "演示场景已创建：\n" + ScenePath +
                "\n\n按 Play 开始攀爬。\n操作：屏幕左右各一个输入区，先按下的一侧决定首个攀爬手；\n拖动触点（编辑器用鼠标）靠近铆钉即可抓握，松手放弃。",
                "OK");
        }

        [MenuItem("Tools/ClimbGame/3C ▸ Create Default Config Assets")]
        public static void CreateDefaultConfigs()
        {
            Directory.CreateDirectory(ConfigDir);
            CreateAsset<ClimbTuningConfig>("ClimbTuningConfig");
            CreateAsset<ArmRigConfig>("ArmRigConfig");
            CreateAsset<HapticConfig>("HapticConfig");
            CreateAsset<MagnifierConfig>("MagnifierConfig");
            CreateAsset<StaminaConfig>("StaminaConfig");
            CreateAsset<RagdollFallConfig>("RagdollFallConfig");
            CreateAsset<ClimbCameraConfig>("ClimbCameraConfig");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("ClimbGame 3C",
                "默认配置资产已生成到：\n" + ConfigDir +
                "\n\n把它们拖到 Climb3CBootstrap 对应字段即可实时调参。", "OK");
        }

        private static void CreateAsset<T>(string name) where T : ScriptableObject
        {
            string path = ConfigDir + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
#endif
