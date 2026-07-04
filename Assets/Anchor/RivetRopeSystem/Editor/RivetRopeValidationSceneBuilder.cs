using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Anchor.RivetRopeSystem.Editor
{
    public static class RivetRopeValidationSceneBuilder
    {
        public const string ScenePath = "Assets/Anchor/RivetRopeSystem/Scenes/RivetRopeValidation.scene";

        [MenuItem("Anchor/Rivet Rope/Rebuild Validation Scene")]
        public static void BuildValidationScene()
        {
            EnsureFolders();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "RivetRopeValidation";

            CreateCamera();
            CreateWall();

            var lower = CreateMarker("Second Player Rope Attach", new Vector3(0f, -3.5f, 0f), Color.cyan);
            var upper = CreateMarker("Lead Player Rope Attach", new Vector3(0f, 5.5f, 0f), new Color(1f, 0.55f, 0.15f));
            var placePoint = CreateMarker("Sample Place Point", new Vector3(0f, 2.4f, 0f), Color.yellow);
            var collectProbe = CreateMarker("Second Collect Probe", new Vector3(0f, 2.4f, -0.4f), Color.cyan);
            CreateInstructionCanvas();

            var systemObject = new GameObject("Rivet Rope System");
            var config = CreateConfigAsset();
            var driver = systemObject.AddComponent<RivetRopeDebugDriver>();
            var line = systemObject.AddComponent<LineRenderer>();
            var visual = systemObject.AddComponent<RivetRopeLineVisual>();
            var panel = systemObject.AddComponent<RivetRopeDebugPanel>();
            var input = systemObject.AddComponent<RivetRopeInputController>();

            ConfigureLine(line);
            BindDriver(driver, config, lower, upper, panel);
            BindVisual(visual, driver, line);
            BindPanel(panel, driver, placePoint, collectProbe);
            BindInput(input, driver, placePoint, collectProbe);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"Rivet rope validation scene rebuilt at {ScenePath}");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Anchor/RivetRopeSystem", "Scenes");
            EnsureFolder("Assets/Anchor/RivetRopeSystem", "Config");
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0.8f, -14f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.06f, 0.08f);
        }

        private static void CreateWall()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Debug Wall";
            wall.transform.position = new Vector3(0f, 1f, 0.15f);
            wall.transform.localScale = new Vector3(8f, 11f, 0.12f);
            SetColor(wall, new Color(0.18f, 0.18f, 0.2f));
        }

        private static Transform CreateMarker(string name, Vector3 position, Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * 0.25f;
            SetColor(marker, color);
            return marker.transform;
        }

        private static RivetRopeConfig CreateConfigAsset()
        {
            const string path = "Assets/Anchor/RivetRopeSystem/Config/RivetRopeValidationConfig.asset";
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
            line.widthMultiplier = 0.04f;
            line.positionCount = 0;
            line.material = new Material(Shader.Find("Sprites/Default"));
        }

        private static void CreateInstructionCanvas()
        {
            var canvasObject = new GameObject("Rivet Rope Validation Instructions");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            var textObject = new GameObject("Instructions");
            textObject.transform.SetParent(canvasObject.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.alignment = TextAnchor.LowerLeft;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text =
                "Rivet Rope Validation\n" +
                "PC: Left Click/E 插锚, R 回收最近铆钉, F 触发坠落窗口, Q 收绳, Tab 换领.\n" +
                "Touch: 左侧插锚, 中间回收, 右侧收绳.\n" +
                "Debug panel stays available for fallback validation.\n" +
                "Second Collect Probe is placed near Sample Place Point for quick collection smoke tests.";

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(16f, 16f);
            rect.sizeDelta = new Vector2(760f, 120f);
        }

        private static void BindDriver(
            RivetRopeDebugDriver driver,
            RivetRopeConfig config,
            Transform lower,
            Transform upper,
            RivetRopeDebugPanel panel)
        {
            var serialized = new SerializedObject(driver);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("leadPlayerId").stringValue = "lead";
            serialized.FindProperty("secondPlayerId").stringValue = "second";
            serialized.FindProperty("lowerAttachPoint").objectReferenceValue = lower;
            serialized.FindProperty("upperAttachPoint").objectReferenceValue = upper;
            serialized.FindProperty("damageSinkSource").objectReferenceValue = panel;
            serialized.FindProperty("logOperations").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindVisual(RivetRopeLineVisual visual, RivetRopeDebugDriver driver, LineRenderer line)
        {
            var serialized = new SerializedObject(visual);
            serialized.FindProperty("driver").objectReferenceValue = driver;
            serialized.FindProperty("lineRenderer").objectReferenceValue = line;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindPanel(RivetRopeDebugPanel panel, RivetRopeDebugDriver driver, Transform placePoint, Transform collectPoint)
        {
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("driver").objectReferenceValue = driver;
            serialized.FindProperty("showPanel").boolValue = true;
            serialized.FindProperty("samplePlacePoint").objectReferenceValue = placePoint;
            serialized.FindProperty("sampleCollectPlayerPoint").objectReferenceValue = collectPoint;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BindInput(
            RivetRopeInputController input,
            RivetRopeDebugDriver driver,
            Transform placePoint,
            Transform collectPoint)
        {
            var serialized = new SerializedObject(input);
            serialized.FindProperty("driver").objectReferenceValue = driver;
            serialized.FindProperty("targetCamera").objectReferenceValue = Camera.main;
            serialized.FindProperty("placeFallbackPoint").objectReferenceValue = placePoint;
            serialized.FindProperty("collectPlayerPoint").objectReferenceValue = collectPoint;
            serialized.FindProperty("logInputActions").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetColor(GameObject gameObject, Color color)
        {
            var renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = new Material(Shader.Find("Standard")) { color = color };
            }
        }
    }
}
