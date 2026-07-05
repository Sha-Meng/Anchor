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
            ConfigureGameplaySettings(config);
            ConfigureConfigVisuals(config);
            var driver = root.AddComponent<RivetRopeDebugDriver>();
            var line = root.AddComponent<LineRenderer>();
            var visual = root.AddComponent<RivetRopeLineVisual>();
            var rivetVisual = root.AddComponent<RivetRopeRivetVisual>();
            var binder = root.AddComponent<RivetRopeMainGameplayBinder>();
            var ui = root.AddComponent<RivetRopeMainGameplayUi>();

            ConfigureLine(line);
            BindDriver(driver, config, lower, upper);
            BindVisual(visual, config, driver, line);
            BindRivetVisual(rivetVisual, config, driver);
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

        private static void ConfigureGameplaySettings(RivetRopeConfig config)
        {
            var settings = RivetRopeSettings.CreateDefault();
            settings.TotalRopeLength = 4.25f;
            settings.EnableForceFeedback = true;
            settings.ForcePreTensionThreshold = 0f;
            settings.ForceMaxConstraintCorrection = 0.85f;
            settings.ForceElasticStretch = 0.55f;
            settings.ForceTensionStrengthPerMeter = 4.8f;
            settings.ForceVelocityDamping = 0.75f;
            settings.ForceReboundStrength = 0.48f;
            settings.ForceMaxFeedbackStrength = 12f;
            config.ConfigureRuntime(settings);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureLine(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.widthMultiplier = 0.055f;
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            line.positionCount = 0;
            line.textureMode = LineTextureMode.Tile;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.material = RivetRopeEditorAssetUtility.EnsureDefaultRopeMaterial();
        }

        private static void ConfigureConfigVisuals(RivetRopeConfig config)
        {
            var visuals = RivetRopeVisualSettings.CreateDefault();
            visuals.RopeMaterial = RivetRopeEditorAssetUtility.EnsureDefaultRopeMaterial();
            config.ConfigureRuntimeVisuals(visuals);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
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

        private static void BindVisual(RivetRopeLineVisual visual, RivetRopeConfig config, RivetRopeDebugDriver driver, LineRenderer line)
        {
            var serialized = new SerializedObject(visual);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("driver").objectReferenceValue = driver;
            serialized.FindProperty("lineRenderer").objectReferenceValue = line;
            serialized.FindProperty("preferConfigSettings").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindRivetVisual(RivetRopeRivetVisual visual, RivetRopeConfig config, RivetRopeDebugDriver driver)
        {
            var serialized = new SerializedObject(visual);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("driver").objectReferenceValue = driver;
            serialized.FindProperty("targetCamera").objectReferenceValue = Camera.main;
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
            serialized.FindProperty("localPlayerRole").intValue = (int)RivetRopeLocalPlayerRole.Lead;
            serialized.FindProperty("waistOffset").vector3Value = new Vector3(0f, -0.45f, 0f);
            serialized.FindProperty("ropeDepthOffset").floatValue = 0f;
            serialized.FindProperty("ropeSideOffset").floatValue = 0f;
            serialized.FindProperty("lowerOffsetFromUpper").vector3Value = new Vector3(0f, -3.2f, 0f);
            serialized.FindProperty("probeFollowSmoothTime").floatValue = 0.08f;
            serialized.FindProperty("autoStartRescueOnFalling").boolValue = true;
            serialized.FindProperty("preferBoneAttachPoints").boolValue = true;
            serialized.FindProperty("strictBoneFollow").boolValue = true;
            serialized.FindProperty("applyPresentationOffsetToAttachPoints").boolValue = false;
            serialized.FindProperty("localAttachBone").intValue = (int)HumanBodyBones.Hips;
            serialized.FindProperty("remoteAttachBone").intValue = (int)HumanBodyBones.Hips;
            serialized.FindProperty("attachBoneNameFallbacks").stringValue = "Spine2,Spine1,Spine,Hips,Torso";
            serialized.FindProperty("boneLocalOffset").vector3Value = Vector3.zero;
            serialized.FindProperty("renderRopeBehindCharacters").boolValue = true;
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
            serialized.FindProperty("panelAnchor").vector2Value = new Vector2(1f, 0.5f);
            serialized.FindProperty("panelPivot").vector2Value = new Vector2(1f, 0.5f);
            serialized.FindProperty("panelOffset").vector2Value = new Vector2(-32f, 0f);
            serialized.FindProperty("showStatus").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
