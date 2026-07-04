using System.Collections.Generic;
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
    /// 把攀爬 3C 接入已有关卡（如 MainLevel）：复用场景现有的攀爬抓点（RouteNetwork 的叶节点）
    /// 作为铆钉、复用 Main Camera，仅程序化生成角色/控制器/UI 并连线。
    /// 不创建墙面/相机/灯光，避免与关卡设计冲突。所有参数经 ScriptableObject 配置。
    /// </summary>
    [AddComponentMenu("ClimbGame/Climb3C Level Binder")]
    public sealed class Climb3CLevelBinder : MonoBehaviour
    {
        [Header("配置（留空则运行时创建默认实例）")]
        public ClimbTuningConfig tuning;
        public ArmRigConfig armRig;
        public HapticConfig haptic;
        public MagnifierConfig magnifier;
        public StaminaConfig stamina;
        public RagdollFallConfig ragdollFall;
        public ClimbCameraConfig cameraConfig;

        [Header("关卡绑定")]
        [Tooltip("攀爬抓点根物体名（其下所有叶节点视为铆钉）")]
        public string routeRootName = "RouteNetwork";

        [Tooltip("角色躯干相对抓点平面向镜头方向的前置距离")]
        public float characterFrontOffset = 0.45f;

        [Tooltip("起攀点相对最低抓点下移的高度")]
        public float startBelowLowest = 0.2f;

        [Tooltip("是否让相机跟随攀爬者上移（保留关卡原有构图角度与偏移）")]
        public bool cameraFollow = true;

        [Header("角色外观")]
        public Color bodyColor = new Color(0.2f, 0.5f, 0.85f);
        public Color handColor = new Color(0.95f, 0.8f, 0.65f);

        private const int LayerIgnoreRaycast = 2;
        private const int LayerUI = 5;

        private ClimbController3D _controller;

        private void Start()
        {
            EnsureConfigs();

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[Climb3CLevelBinder] 场景中未找到 MainCamera，无法绑定攀爬 3C。");
                return;
            }

            // --- 收集现有抓点作为铆钉 ---
            var rivetFieldGo = new GameObject("Climb3C_RivetField");
            var rivetField = rivetFieldGo.AddComponent<RivetField>();

            var nodes = new List<Transform>();
            GameObject routeRoot = GameObject.Find(routeRootName);
            if (routeRoot != null) CollectLeafPoints(routeRoot.transform, nodes);
            if (nodes.Count == 0)
            {
                Debug.LogError($"[Climb3CLevelBinder] 在 '{routeRootName}' 下未找到任何攀爬抓点叶节点。");
                return;
            }

            Vector3 min = nodes[0].position;
            Vector3 max = nodes[0].position;
            float sumZ = 0f, sumX = 0f;
            foreach (Transform n in nodes)
            {
                Vector3 p = n.position;
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
                sumZ += p.z;
                sumX += p.x;

                var rivet = n.gameObject.GetComponent<RivetPoint>();
                if (rivet == null) rivet = n.gameObject.AddComponent<RivetPoint>();
                if (haptic != null)
                {
                    rivet.previewSnapRadius = haptic.snapRadius;
                    rivet.previewIntenseRadius = haptic.intenseRadius;
                    rivet.previewSlightRadius = haptic.slightRadius;
                }
                rivetField.Register(rivet);
            }

            float planeZ = sumZ / nodes.Count;
            float centerX = sumX / nodes.Count;
            Vector3 startCenter = new Vector3(centerX, min.y - startBelowLowest, planeZ - characterFrontOffset);

            // --- 材质 ---
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            Material bodyMat = new Material(shader) { color = bodyColor };
            Material handMat = new Material(shader) { color = handColor };

            // --- 角色 ---
            var character = new ClimbCharacter(armRig, ragdollFall);
            character.Build(null, startCenter, bodyMat, handMat);
            SetLayerRecursive(character.Root, LayerIgnoreRaycast);

            // --- Canvas / UI ---
            var canvasGo = new GameObject("Climb3C_Canvas");
            canvasGo.layer = LayerUI;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var zoneOverlay = new GameObject("InputZoneOverlayUI").AddComponent<InputZoneOverlayUI>();
            zoneOverlay.transform.SetParent(canvasGo.transform, false);
            zoneOverlay.Build(canvas, tuning);

            var staminaBar = new GameObject("StaminaBarUI").AddComponent<StaminaBarUI>();
            staminaBar.transform.SetParent(canvasGo.transform, false);
            staminaBar.Build(canvas);

            // --- 服务 ---
            var servicesGo = new GameObject("Climb3C_Services");
            var input = servicesGo.AddComponent<ClimbTouchInput>();
            input.SetTuning(tuning);

            var hapticAdapter = servicesGo.AddComponent<MobileHapticFeedbackAdapter>();
            var haptics = servicesGo.AddComponent<HapticService>();
            haptics.Configure(haptic, hapticAdapter);

            var magnifierGo = new GameObject("HandMagnifier");
            magnifierGo.transform.SetParent(servicesGo.transform, false);
            var magnifierComp = magnifierGo.AddComponent<HandMagnifier>();
            magnifierComp.Setup(magnifier, cam, canvas, LayerUI);

            // 触点映射到抓点所在平面（抓点无碰撞体，用平面投影，mask=0 强制走平面回退）
            var projector = new WallProjector(cam, 0, planeZ);
            var gameContext = new GameContext(0);

            // --- 相机跟随（保留关卡原有角度/偏移）---
            ClimbCamera climbCam = null;
            if (cameraFollow)
            {
                climbCam = cam.GetComponent<ClimbCamera>();
                if (climbCam == null) climbCam = cam.gameObject.AddComponent<ClimbCamera>();
                cameraConfig.pitch = cam.transform.eulerAngles.x;
                cameraConfig.followOffset = cam.transform.position - startCenter;
                cameraConfig.fieldOfView = cam.fieldOfView;
                cameraConfig.lockHorizontal = false;
                climbCam.Configure(cameraConfig, () => _controller != null ? _controller.TorsoCenter : startCenter);
            }

            // --- 控制器 ---
            var controllerGo = new GameObject("Climb3C_Controller");
            _controller = controllerGo.AddComponent<ClimbController3D>();
            _controller.Initialize(gameContext, 0, tuning, armRig, stamina, haptic, character, input,
                projector, rivetField, haptics, magnifierComp, staminaBar, startCenter);
            _controller.SetFallDependencies(ragdollFall, climbCam);
            _controller.SetZoneOverlay(zoneOverlay);

            Debug.Log($"[Climb3CLevelBinder] 攀爬 3C 已绑定到关卡：抓点 {nodes.Count} 个，起攀点 {startCenter}。");
        }

        /// <summary>递归收集叶节点（无子物体的 Transform）作为抓点。</summary>
        private static void CollectLeafPoints(Transform root, List<Transform> outList)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (c.childCount == 0) outList.Add(c);
                else CollectLeafPoints(c, outList);
            }
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
            for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
        }
    }
}
