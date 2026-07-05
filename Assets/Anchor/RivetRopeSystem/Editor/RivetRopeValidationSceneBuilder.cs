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
        public const string VisualLabScenePath = "Assets/Anchor/RivetRopeSystem/Scenes/RivetRopeVisualLab.scene";
        private const string VisualLabCharacterPath = "Assets/Thridpart/PolyOne/FreeStickman/Prefabs/MainAcotor_F.prefab";
        private const string VisualLabFallbackCharacterPath = "Assets/Art/Character/RagDollMan.prefab";

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
            var rivetVisual = systemObject.AddComponent<RivetRopeRivetVisual>();
            var panel = systemObject.AddComponent<RivetRopeDebugPanel>();
            var input = systemObject.AddComponent<RivetRopeInputController>();

            ConfigureLine(line);
            ConfigureConfigVisuals(config);
            BindDriver(driver, config, lower, upper, panel);
            BindVisual(visual, config, driver, line);
            BindRivetVisual(rivetVisual, config, driver);
            BindPanel(panel, driver, placePoint, collectProbe);
            BindInput(input, driver, placePoint, collectProbe);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"Rivet rope validation scene rebuilt at {ScenePath}");
        }

        [MenuItem("Anchor/Rivet Rope/Rebuild Visual Lab Scene")]
        public static void BuildVisualLabScene()
        {
            EnsureFolders();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "RivetRopeVisualLab";

            var camera = CreateVisualLabCamera();
            CreateVisualLabLight();
            CreateVisualLabBackdrop();
            CreateDistanceTicks();

            var lower = CreateMarker("Lower Waist Endpoint", new Vector3(-2.4f, -3.2f, 0f), Color.cyan, 0.34f);
            var upper = CreateMarker("Upper Waist Endpoint", new Vector3(2.2f, 4.8f, 0f), new Color(1f, 0.55f, 0.15f), 0.34f);
            var feedbackProxy = CreateMarker("Rope Force Feedback Proxy", upper.position, new Color(1f, 0.2f, 0.85f), 0.28f);
            var character = CreateVisualLabCharacter(upper.position, out var forceBone);
            var rivets = new[]
            {
                CreateMarker("Lab Rivet 01", new Vector3(0f, 0.9f, 0f), Color.yellow, 0.24f),
                CreateMarker("Lab Rivet 02", new Vector3(0.9f, 1.5f, 0f), Color.yellow, 0.24f),
                CreateMarker("Lab Rivet 03", new Vector3(-0.2f, 3.2f, 0f), Color.yellow, 0.24f),
                CreateMarker("Lab Rivet 04", new Vector3(1.4f, 3.9f, 0f), Color.yellow, 0.24f)
            };

            CreateVisualLabInstructions();

            var systemObject = new GameObject("Rivet Rope Visual Lab System");
            var config = CreateConfigAsset("Assets/Anchor/RivetRopeSystem/Config/RivetRopeVisualLabConfig.asset");
            ConfigureConfigVisuals(config);
            var driver = systemObject.AddComponent<RivetRopeDebugDriver>();
            var line = systemObject.AddComponent<LineRenderer>();
            var visual = systemObject.AddComponent<RivetRopeLineVisual>();
            var rivetVisual = systemObject.AddComponent<RivetRopeRivetVisual>();
            var lab = systemObject.AddComponent<RivetRopeVisualLabController>();

            ConfigureLine(line);
            BindDriver(driver, config, lower, upper, null);
            BindVisual(visual, config, driver, line);
            BindRivetVisual(rivetVisual, config, driver);
            BindVisualLab(lab, config, driver, visual, camera, lower, upper, rivets, feedbackProxy, character, forceBone);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, VisualLabScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"Rivet rope visual lab scene rebuilt at {VisualLabScenePath}");
        }

        private static void EnsureFolders()
        {
            RivetRopeEditorAssetUtility.EnsureCoreFolders();
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

        private static Camera CreateVisualLabCamera()
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0.8f, -15f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.045f, 0.05f, 0.065f);
            return camera;
        }

        private static void CreateVisualLabLight()
        {
            var lightObject = new GameObject("Visual Lab Key Light");
            lightObject.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.96f, 0.9f);
        }

        private static void CreateWall()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Debug Wall";
            wall.transform.position = new Vector3(0f, 1f, 0.15f);
            wall.transform.localScale = new Vector3(8f, 11f, 0.12f);
            SetColor(wall, new Color(0.18f, 0.18f, 0.2f));
        }

        private static void CreateVisualLabBackdrop()
        {
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backdrop.name = "Visual Lab Backdrop";
            backdrop.transform.position = new Vector3(0f, 0.9f, 0.22f);
            backdrop.transform.localScale = new Vector3(9.5f, 11.5f, 0.08f);
            SetColor(backdrop, new Color(0.13f, 0.135f, 0.15f));
        }

        private static void CreateDistanceTicks()
        {
            for (int y = -4; y <= 5; y++)
            {
                var tick = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tick.name = $"Distance Tick {y:+0;-0;0}";
                tick.transform.position = new Vector3(-4.25f, y, -0.04f);
                tick.transform.localScale = new Vector3(y == 0 ? 0.7f : 0.42f, 0.025f, 0.025f);
                SetColor(tick, y == 0 ? new Color(0.5f, 0.62f, 0.78f) : new Color(0.32f, 0.36f, 0.42f));
            }
        }

        private static Transform CreateMarker(string name, Vector3 position, Color color)
        {
            return CreateMarker(name, position, color, 0.25f);
        }

        private static Transform CreateMarker(string name, Vector3 position, Color color, float size)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * size;
            SetColor(marker, color);
            return marker.transform;
        }

        private static RivetRopeConfig CreateConfigAsset()
        {
            return CreateConfigAsset("Assets/Anchor/RivetRopeSystem/Config/RivetRopeValidationConfig.asset");
        }

        private static RivetRopeConfig CreateConfigAsset(string path)
        {
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
            line.widthMultiplier = 0.055f;
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

        private static void CreateVisualLabInstructions()
        {
            var canvasObject = new GameObject("Rivet Rope Visual Lab Instructions");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            var textObject = new GameObject("Instructions");
            textObject.transform.SetParent(canvasObject.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 15;
            text.alignment = TextAnchor.LowerRight;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.text =
                "Rivet Rope Visual Lab\n" +
                "左侧面板切换路径/模式/参数。\n" +
                "拖动青色或橙色端点观察跟随、阻尼和回弹。\n" +
                "黄色球体是铆钉路径预设点。\n" +
                "角色受力骨骼优先绑定 Spine2，回退 Spine1/Spine/Hips。\n" +
                "粉色球体保留为骨骼受力点的辅助观察标记。";

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-16f, 16f);
            rect.sizeDelta = new Vector2(420f, 96f);
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

        private static void BindVisualLab(
            RivetRopeVisualLabController lab,
            RivetRopeConfig config,
            RivetRopeDebugDriver driver,
            RivetRopeLineVisual visual,
            Camera camera,
            Transform lower,
            Transform upper,
            Transform[] rivets,
            Transform feedbackProxy,
            Transform characterRoot,
            Transform forceTargetBone)
        {
            var serialized = new SerializedObject(lab);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("driver").objectReferenceValue = driver;
            serialized.FindProperty("visual").objectReferenceValue = visual;
            serialized.FindProperty("targetCamera").objectReferenceValue = camera;
            serialized.FindProperty("lowerEndpoint").objectReferenceValue = lower;
            serialized.FindProperty("upperEndpoint").objectReferenceValue = upper;
            serialized.FindProperty("feedbackProxy").objectReferenceValue = feedbackProxy;
            serialized.FindProperty("characterRoot").objectReferenceValue = characterRoot;
            serialized.FindProperty("forceTargetBone").objectReferenceValue = forceTargetBone;

            var markers = serialized.FindProperty("rivetMarkers");
            markers.arraySize = rivets.Length;
            for (int i = 0; i < rivets.Length; i++)
            {
                markers.GetArrayElementAtIndex(i).objectReferenceValue = rivets[i];
            }

            serialized.FindProperty("autoMoveUpper").boolValue = true;
            serialized.FindProperty("autoMoveAmplitude").floatValue = 1.6f;
            serialized.FindProperty("autoMoveSpeed").floatValue = 0.65f;
            serialized.FindProperty("enableForceFeedbackPreview").boolValue = true;
            serialized.FindProperty("feedbackDrivesUpperEndpoint").boolValue = characterRoot != null && forceTargetBone != null;
            serialized.FindProperty("useCharacterForceTarget").boolValue = characterRoot != null && forceTargetBone != null;
            serialized.FindProperty("fallGravity").floatValue = 2f;
            serialized.FindProperty("fallInitialDownSpeed").floatValue = 0f;
            serialized.FindProperty("maxFallSpeed").floatValue = 6f;
            serialized.FindProperty("fallCatchImpulse").floatValue = 0.65f;
            serialized.FindProperty("fallCatchSpring").floatValue = 7.5f;
            serialized.FindProperty("fallCatchDamping").floatValue = 1.8f;
            serialized.FindProperty("fallCatchImpulseStretch").floatValue = 0.28f;
            serialized.FindProperty("loopFallCatchPreview").boolValue = true;
            serialized.FindProperty("fallReplayDelay").floatValue = 1.4f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform CreateVisualLabCharacter(Vector3 forceTargetPosition, out Transform forceBone)
        {
            forceBone = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualLabCharacterPath);
            if (prefab == null)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualLabFallbackCharacterPath);
            }

            if (prefab == null)
            {
                Debug.LogWarning("Rivet rope visual lab: character prefab not found, falling back to proxy-only validation.");
                return null;
            }

            var character = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (character == null)
            {
                return null;
            }

            character.name = "Rope Force Test Character (MainAcotor_F)";
            character.transform.position = new Vector3(forceTargetPosition.x, forceTargetPosition.y, 0f);
            character.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            character.transform.localScale = Vector3.one * 2.6f;
            DisableRuntimeCharacterScripts(character);

            forceBone =
                FindDeep(character.transform, "Spine2") ??
                FindDeep(character.transform, "Spine1") ??
                FindDeep(character.transform, "Spine") ??
                FindDeep(character.transform, "Hips") ??
                character.transform;

            character.transform.position += forceTargetPosition - forceBone.position;
            Debug.Log($"Rivet rope visual lab character force target: {forceBone.name}");
            return character.transform;
        }

        private static void DisableRuntimeCharacterScripts(GameObject character)
        {
            var behaviours = character.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                behaviours[i].enabled = false;
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
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
