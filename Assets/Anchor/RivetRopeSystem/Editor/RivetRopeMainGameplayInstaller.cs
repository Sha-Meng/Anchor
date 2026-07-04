using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Anchor.RivetRopeSystem.Editor
{
    public static class RivetRopeMainGameplayInstaller
    {
        private const string MainScenePath = "Assets/Scenes/MainLevel.scene";
        private const string RootName = "Rivet Rope Main Gameplay";

        [MenuItem("Anchor/Rivet Rope/Install In MainLevel")]
        public static void InstallInMainLevel()
        {
            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            var root = new GameObject(RootName);
            var upper = CreateProbe("Rivet Rope Upper Attach", root.transform, new Vector3(0f, 2f, 0f));
            var lower = CreateProbe("Rivet Rope Lower Attach", root.transform, new Vector3(0f, -1.2f, 0f));
            var place = CreateProbe("Rivet Rope Place Probe", root.transform, new Vector3(0f, 2.6f, 0f));
            var collect = CreateProbe("Rivet Rope Collect Probe", root.transform, new Vector3(0f, 2.6f, -0.2f));

            var config = CreateConfigAsset();
            var driver = root.AddComponent<RivetRopeDebugDriver>();
            var line = root.AddComponent<LineRenderer>();
            var visual = root.AddComponent<RivetRopeLineVisual>();
            var binder = root.AddComponent<RivetRopeMainGameplayBinder>();
            var ui = root.AddComponent<RivetRopeMainGameplayUi>();

            ConfigureLine(line);
            BindDriver(driver, config, lower, upper);
            BindVisual(visual, driver, line);
            BindMainBinder(binder, driver, upper, lower, place, collect);
            BindMainUi(ui, driver, binder);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.Refresh();
            Debug.Log($"Rivet rope main gameplay installed in {MainScenePath}");
        }

        private static Transform CreateProbe(string name, Transform parent, Vector3 position)
        {
            var probe = new GameObject(name);
            probe.name = name;
            probe.transform.SetParent(parent, false);
            probe.transform.position = position;
            return probe.transform;
        }

        private static RivetRopeConfig CreateConfigAsset()
        {
            const string folder = "Assets/Anchor/RivetRopeSystem/Config";
            const string path = folder + "/RivetRopeMainGameplayConfig.asset";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/Anchor/RivetRopeSystem", "Config");
            }

            var config = AssetDatabase.LoadAssetAtPath<RivetRopeConfig>(path);
            if (config != null)
            {
                return config;
            }

            if (File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            config = ScriptableObject.CreateInstance<RivetRopeConfig>();
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static void ConfigureLine(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.widthMultiplier = 1f;
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 0.016548652f);
            line.positionCount = 0;
            var ropeMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/FImpossible Creations/Plugins - Animating/Ragdoll Animator 2/Ragdoll Animator 2 - Demo/Demos Assets/Materials/MAT_Demo_Wood 2.mat");
            line.material = ropeMaterial != null
                ? ropeMaterial
                : new Material(Shader.Find("Sprites/Default"));
        }

        private static void BindDriver(RivetRopeDebugDriver driver, RivetRopeConfig config, Transform lower, Transform upper)
        {
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("leadPlayerId").stringValue = "lead";
            serialized.FindProperty("secondPlayerId").stringValue = "second";
            serialized.FindProperty("lowerAttachPoint").objectReferenceValue = lower;
            serialized.FindProperty("upperAttachPoint").objectReferenceValue = upper;
            serialized.FindProperty("logOperations").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindVisual(RivetRopeLineVisual visual, RivetRopeDebugDriver driver, LineRenderer line)
        {
            var serialized = new SerializedObject(visual);
            serialized.FindProperty("driver").objectReferenceValue = driver;
            serialized.FindProperty("lineRenderer").objectReferenceValue = line;
            serialized.FindProperty("slackSagPerMeter").floatValue = 0.09f;
            serialized.FindProperty("maxSag").floatValue = 1.1f;
            serialized.FindProperty("segmentSubdivisions").intValue = 12;
            serialized.FindProperty("enableDynamics").boolValue = true;
            serialized.FindProperty("springSmoothTime").floatValue = 0.18f;
            serialized.FindProperty("physicsGravity").floatValue = 7.5f;
            serialized.FindProperty("physicsDamping").floatValue = 0.94f;
            serialized.FindProperty("targetFollow").floatValue = 6.5f;
            serialized.FindProperty("constraintIterations").intValue = 6;
            serialized.FindProperty("swayAmplitude").floatValue = 0.075f;
            serialized.FindProperty("swaySpeed").floatValue = 1.8f;
            serialized.FindProperty("slackColor").colorValue = Color.white;
            serialized.FindProperty("tautColor").colorValue = Color.white;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindMainBinder(
            RivetRopeMainGameplayBinder binder,
            RivetRopeDebugDriver driver,
            Transform upper,
            Transform lower,
            Transform place,
            Transform collect)
        {
            var serialized = new SerializedObject(binder);
            serialized.FindProperty("driver").objectReferenceValue = driver;
            serialized.FindProperty("upperAttachPoint").objectReferenceValue = upper;
            serialized.FindProperty("lowerAttachPoint").objectReferenceValue = lower;
            serialized.FindProperty("placeFallbackPoint").objectReferenceValue = place;
            serialized.FindProperty("collectProbePoint").objectReferenceValue = collect;
            serialized.FindProperty("targetCamera").objectReferenceValue = Camera.main;
            serialized.FindProperty("waistOffset").vector3Value = new Vector3(0f, -0.75f, 0f);
            serialized.FindProperty("ropeDepthOffset").floatValue = 0.24f;
            serialized.FindProperty("ropeSideOffset").floatValue = 0.36f;
            serialized.FindProperty("lowerOffsetFromUpper").vector3Value = new Vector3(0f, -3.2f, 0f);
            serialized.FindProperty("probeFollowSmoothTime").floatValue = 0.14f;
            serialized.FindProperty("autoStartRescueOnFalling").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindMainUi(
            RivetRopeMainGameplayUi ui,
            RivetRopeDebugDriver driver,
            RivetRopeMainGameplayBinder binder)
        {
            var serialized = new SerializedObject(ui);
            serialized.FindProperty("driver").objectReferenceValue = driver;
            serialized.FindProperty("binder").objectReferenceValue = binder;
            serialized.FindProperty("panelOffset").vector2Value = new Vector2(-32f, 220f);
            serialized.FindProperty("showStatus").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
