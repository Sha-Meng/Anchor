using System.IO;
using Anchor.LevelAnchorSystem;
using DesignerSpace;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Anchor.SystemValidation.Editor
{
    public static class SystemValidationSceneBuilder
    {
        public const string ScenePath = "Assets/Anchor/SystemValidation/Scenes/SystemValidationTest.scene";
        public const string AndroidApkPath = "out/Android/SystemValidationTest.apk";

        [MenuItem("Anchor/System Validation/Rebuild Test Scene")]
        public static void BuildSystemValidationScene()
        {
            EnsureFolders();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "SystemValidationTest";

            var camera = CreateCamera();
            var wall = CreateWall();
            var anchorsRoot = new GameObject("Anchors");
            CreateAnchor(anchorsRoot.transform, "Anchor_High_Left", new Vector3(-2.5f, 2.2f, -0.22f), 10, 0.75f, 2.5f, Color.cyan);
            CreateAnchor(anchorsRoot.transform, "Anchor_High_Right", new Vector3(2.5f, 2.2f, -0.22f), 10, 0.75f, 2.5f, Color.cyan);
            CreateAnchor(anchorsRoot.transform, "Anchor_Low_Left", new Vector3(-2.5f, -1.2f, -0.22f), 4, 0.5f, 2.2f, new Color(1f, 0.75f, 0.15f));
            CreateAnchor(anchorsRoot.transform, "Anchor_Low_Right", new Vector3(2.5f, -1.2f, -0.22f), 4, 0.5f, 2.2f, new Color(1f, 0.75f, 0.15f));
            CreateAnchor(anchorsRoot.transform, "Anchor_Edge_Test", new Vector3(0f, 4.2f, -0.22f), 7, 0.5f, 1.6f, new Color(0.4f, 1f, 0.5f));

            var reservedRoot = new GameObject("Reserved Obstacle And Fake Samples");
            reservedRoot.transform.position = new Vector3(0f, -4f, -0.18f);
            CreateReservedMarker(reservedRoot.transform, "Reserved_Obstacle_Area", new Vector3(-2.2f, 0f, 0f), new Color(1f, 0.2f, 0.2f, 0.35f));
            CreateReservedMarker(reservedRoot.transform, "Reserved_Fake_Area", new Vector3(2.2f, 0f, 0f), new Color(0.7f, 0.2f, 1f, 0.35f));

            var registry = CreateRegistry(wall.transform);
            var leftMarker = CreateHandMarker("Left Input Marker", new Color(0.1f, 0.9f, 1f));
            var rightMarker = CreateHandMarker("Right Input Marker", new Color(1f, 0.45f, 0.1f));
            var debugPanel = CreateDebugCanvas();

            var controllerObject = new GameObject("SystemValidationController");
            var controller = controllerObject.AddComponent<SystemValidationController>();
            var haptics = controllerObject.AddComponent<MobileHapticFeedbackAdapter>();
            BindController(controller, camera, registry, leftMarker, rightMarker, debugPanel, haptics);

            registry.RebuildFromScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"System validation test scene rebuilt at {ScenePath}");
        }

        [MenuItem("Anchor/System Validation/Build Android APK")]
        public static void BuildAndroidApk()
        {
            var absoluteOutputPath = Path.GetFullPath(AndroidApkPath);
            var outputDirectory = Path.GetDirectoryName(absoluteOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            if (!File.Exists(ScenePath))
            {
                BuildSystemValidationScene();
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = absoluteOutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"SystemValidationTest Android APK build failed: {summary.result}, errors={summary.totalErrors}, warnings={summary.totalWarnings}");
            }

            Debug.Log(
                $"SystemValidationTest Android APK built: {absoluteOutputPath}, size={summary.totalSize} bytes, warnings={summary.totalWarnings}");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Anchor/SystemValidation", "Scenes");
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -14f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            return camera;
        }

        private static GameObject CreateWall()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Validation Wall";
            wall.transform.position = Vector3.zero;
            wall.transform.localScale = new Vector3(10f, 10f, 0.2f);
            SetRendererColor(wall, new Color(0.18f, 0.18f, 0.2f));
            return wall;
        }

        private static void CreateAnchor(
            Transform parent,
            string name,
            Vector3 position,
            int stability,
            float intenseRadius,
            float slightRadius,
            Color color)
        {
            var anchor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            anchor.name = name;
            anchor.transform.SetParent(parent);
            anchor.transform.position = position;
            anchor.transform.localScale = Vector3.one * 0.28f;
            SetRendererColor(anchor, color);
            RemoveCollider(anchor);

            var anchorPoint = anchor.AddComponent<AnchorPoint>();
            anchorPoint.baseStability = stability;
            anchorPoint.previewIntenseRadius = intenseRadius;
            anchorPoint.previewSlightRadius = slightRadius;
        }

        private static void CreateReservedMarker(Transform parent, string name, Vector3 localPosition, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = name;
            marker.transform.SetParent(parent);
            marker.transform.localPosition = localPosition;
            marker.transform.localScale = new Vector3(1.4f, 1.0f, 0.08f);
            SetRendererColor(marker, color);
            RemoveCollider(marker);
        }

        private static SystemValidationLevelAnchorRegistry CreateRegistry(Transform planeReference)
        {
            var registryObject = new GameObject("LevelAnchorRegistry");
            var registry = registryObject.AddComponent<SystemValidationLevelAnchorRegistry>();
            var serialized = new SerializedObject(registry);
            serialized.FindProperty("rebuildOnAwake").boolValue = true;
            serialized.FindProperty("planeReference").objectReferenceValue = planeReference;
            serialized.FindProperty("logInitialization").boolValue = true;
            serialized.FindProperty("drawGizmos").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return registry;
        }

        private static Transform CreateHandMarker(string name, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.localScale = Vector3.one * 0.22f;
            SetRendererColor(marker, color);
            RemoveCollider(marker);
            marker.SetActive(false);
            return marker.transform;
        }

        private static SystemValidationDebugPanel CreateDebugCanvas()
        {
            var canvasObject = new GameObject("System Validation Debug UI");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            var textObject = new GameObject("Debug Text");
            textObject.transform.SetParent(canvasObject.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(760f, 520f);

            var panel = canvasObject.AddComponent<SystemValidationDebugPanel>();
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("outputText").objectReferenceValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return panel;
        }

        private static void BindController(
            SystemValidationController controller,
            Camera camera,
            SystemValidationLevelAnchorRegistry registry,
            Transform leftMarker,
            Transform rightMarker,
            SystemValidationDebugPanel debugPanel,
            MobileHapticFeedbackAdapter haptics)
        {
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("targetCamera").objectReferenceValue = camera;
            serialized.FindProperty("wallMask").intValue = ~0;
            serialized.FindProperty("gripQueryProviderSource").objectReferenceValue = registry;
            serialized.FindProperty("levelAnchorQuerySource").objectReferenceValue = registry;
            serialized.FindProperty("gripQueryRadius").floatValue = 0.25f;
            serialized.FindProperty("nearestAnchorSearchDistance").floatValue = 10f;
            serialized.FindProperty("leftMarker").objectReferenceValue = leftMarker;
            serialized.FindProperty("rightMarker").objectReferenceValue = rightMarker;
            serialized.FindProperty("debugPanel").objectReferenceValue = debugPanel;
            serialized.FindProperty("haptics").objectReferenceValue = haptics;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRendererColor(GameObject gameObject, Color color)
        {
            var renderer = gameObject.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var material = new Material(Shader.Find("Standard"))
            {
                color = color
            };
            renderer.sharedMaterial = material;
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            var collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }
    }
}
