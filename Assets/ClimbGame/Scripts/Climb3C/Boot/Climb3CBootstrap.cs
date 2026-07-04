using Anchor.SystemValidation;
using ClimbGame.Climb3C.Character;
using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using ClimbGame.Climb3C.Feedback;
using ClimbGame.Climb3C.Gameplay;
using ClimbGame.Climb3C.Input;
using ClimbGame.Climb3C.State;
using ClimbGame.Climb3C.UI;
using UnityEngine;

namespace ClimbGame.Climb3C.Boot
{
    /// <summary>
    /// 攀爬 3C 组合根：程序化创建相机/灯光/墙面/铆钉/角色/UI 并把各子系统连线。
    /// 直接把本组件挂到空物体上 Play 即可运行；所有参数经 ScriptableObject 配置。
    /// </summary>
    [AddComponentMenu("ClimbGame/Climb3C Bootstrap")]
    public sealed class Climb3CBootstrap : MonoBehaviour
    {
        [Header("配置（留空则运行时创建默认实例）")]
        public ClimbTuningConfig tuning;
        public ArmRigConfig armRig;
        public HapticConfig haptic;
        public MagnifierConfig magnifier;
        public StaminaConfig stamina;
        public RagdollFallConfig ragdollFall;
        public ClimbCameraConfig cameraConfig;

        [Header("墙面")]
        public Vector2 wallSize = new Vector2(6f, 16f);
        public Color wallColor = new Color(0.28f, 0.31f, 0.36f);
        public Color rivetColor = new Color(0.85f, 0.55f, 0.2f);
        public Color bodyColor = new Color(0.2f, 0.5f, 0.85f);
        public Color handColor = new Color(0.95f, 0.8f, 0.65f);

        private const int LayerDefault = 0;
        private const int LayerIgnoreRaycast = 2;
        private const int LayerUI = 5;
        private const float RivetZ = -0.3f;

        private ClimbController3D _controller;

        private void Start()
        {
            EnsureConfigs();

            // --- 材质 ---
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            Material wallMat = new Material(shader) { color = wallColor };
            Material rivetMat = new Material(shader) { color = rivetColor };
            Material bodyMat = new Material(shader) { color = bodyColor };
            Material handMat = new Material(shader) { color = handColor };

            // --- 灯光 ---
            var lightGo = new GameObject("ClimbDirectionalLight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(35f, -20f, 0f);
            light.intensity = 1.05f;

            // --- 相机 ---
            var camGo = new GameObject("ClimbMainCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.1f, 0.13f);
            camGo.AddComponent<AudioListener>();
            var climbCam = camGo.AddComponent<ClimbCamera>();

            // --- 墙面 ---
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "ClimbWall";
            wall.transform.position = new Vector3(0f, wallSize.y * 0.5f - 4f, 0f);
            wall.transform.localScale = new Vector3(wallSize.x, wallSize.y, 0.4f);
            wall.layer = LayerDefault;
            wall.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            var rivetField = wall.AddComponent<RivetField>();

            // --- 铆钉 ---
            BuildRivets(wall.transform, rivetMat, rivetField);
            rivetField.RefreshFromScene();

            // --- 角色 ---
            Vector3 startCenter = new Vector3(0f, 0f, 0f) + tuning.startCenterOffset;
            var character = new ClimbCharacter(armRig, ragdollFall);
            character.Build(null, startCenter, bodyMat, handMat);
            SetLayerRecursive(character.Root, LayerIgnoreRaycast);

            // --- Canvas / UI ---
            var canvasGo = new GameObject("ClimbCanvas");
            canvasGo.layer = LayerUI;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var staminaBar = new GameObject("StaminaBarUI").AddComponent<StaminaBarUI>();
            staminaBar.transform.SetParent(canvasGo.transform, false);
            staminaBar.Build(canvas, stamina, cam);

            // --- 服务 ---
            var servicesGo = new GameObject("ClimbServices");
            var input = servicesGo.AddComponent<ClimbTouchInput>();
            input.SetTuning(tuning);

            var hapticAdapter = servicesGo.AddComponent<MobileHapticFeedbackAdapter>();
            var haptics = servicesGo.AddComponent<HapticService>();
            haptics.Configure(haptic, hapticAdapter);

            var magnifierGo = new GameObject("HandMagnifier");
            magnifierGo.transform.SetParent(servicesGo.transform, false);
            var magnifierComp = magnifierGo.AddComponent<HandMagnifier>();
            magnifierComp.Setup(magnifier, cam, canvas, LayerUI);

            var projector = new WallProjector(cam, 1 << LayerDefault, RivetZ);

            // --- 相机连线（越肩 + 二次 lookat）---
            climbCam.Configure(cameraConfig, character);

            // --- 运行时上下文（保存攀爬者运行时数据，供后续联机同步）---
            var gameContext = new GameContext(0);

            // --- 控制器 ---
            var controllerGo = new GameObject("ClimbController");
            _controller = controllerGo.AddComponent<ClimbController3D>();
            _controller.Initialize(gameContext, 0, tuning, armRig, stamina, haptic, character, input,
                projector, rivetField, haptics, magnifierComp, staminaBar, startCenter);
            _controller.SetFallDependencies(ragdollFall, climbCam);
            _controller.SetCameraConfig(cameraConfig);

            int wallMask = ~((1 << LayerIgnoreRaycast) | (1 << LayerUI));
            _controller.SetWallProbe(new WallDepthProbe(wallMask, RivetZ - 4f, 8f, 0.05f));

            // 胶囊体防穿模：躯干胶囊体与墙体重叠时沿碰撞法线推出贴合（排除角色/UI 层）
            if (character.BodyCapsule != null)
            {
                _controller.SetWallResolver(new CapsuleWallResolver(character.BodyCapsule, wallMask));
            }
        }

        private void BuildRivets(Transform wallParent, Material rivetMat, RivetField field)
        {
            // 在墙面上按错落网格布铆钉，覆盖起攀中心到上方，供连续攀爬
            float[] rows = { -2f, -1f, 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f };
            for (int i = 0; i < rows.Length; i++)
            {
                float y = rows[i];
                bool even = i % 2 == 0;
                float[] xs = even ? new[] { -1.4f, 0f, 1.4f } : new[] { -0.8f, 0.8f };
                foreach (float x in xs)
                {
                    CreateRivet(new Vector3(x, y, RivetZ), rivetMat, field);
                }
            }
        }

        private void CreateRivet(Vector3 pos, Material rivetMat, RivetField field)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Rivet";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.24f;
            go.layer = LayerDefault;
            go.GetComponent<MeshRenderer>().sharedMaterial = rivetMat;
            var rivet = go.AddComponent<RivetPoint>();
            if (haptic != null)
            {
                rivet.previewSnapRadius = haptic.snapRadius;
                rivet.previewIntenseRadius = haptic.intenseRadius;
                rivet.previewSlightRadius = haptic.slightRadius;
            }
            field.Register(rivet);
        }

        private void EnsureConfigs()
        {
            if (tuning == null) tuning = ScriptableObject.CreateInstance<ClimbTuningConfig>();
            if (armRig == null) armRig = ScriptableObject.CreateInstance<ArmRigConfig>();
            if (haptic == null) haptic = ScriptableObject.CreateInstance<HapticConfig>();
            if (magnifier == null) magnifier = ScriptableObject.CreateInstance<MagnifierConfig>();
            if (stamina == null) stamina = ScriptableObject.CreateInstance<StaminaConfig>();
            if (ragdollFall == null) ragdollFall = ScriptableObject.CreateInstance<RagdollFallConfig>();
            if (cameraConfig == null) cameraConfig = ScriptableObject.CreateInstance<ClimbCameraConfig>();
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
            {
                SetLayerRecursive(t.GetChild(i), layer);
            }
        }
    }
}
