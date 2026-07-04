using System.Collections;
using System.Collections.Generic;
using Anchor.ForceSystem;
using Anchor.LevelAnchorSystem;
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

        [Tooltip("起攀点相对最低抓点下移的高度（未指定初始双手抓点时使用）")]
        public float startBelowLowest = 0.2f;

        [Tooltip("使用网络/配置指定的起攀中心，而不是根据抓点自动推导")]
        public bool useConfiguredStartCenter;

        [Tooltip("网络/配置指定的起攀中心。用于房主上方、非房主下方开局")]
        public Vector3 configuredStartCenter;

        [Tooltip("网络/配置指定的起攀主抓点名；启用后会自动寻找最近邻抓点组成左右手初始抓握")]
        public string primaryStartAnchorName;

        [Tooltip("根据起攀主抓点自动选择最近邻抓点作为另一只手")]
        public bool useNearestStartAnchorPair;

        [Header("初始双手抓点（按场景物体名查找；留空则用默认起攀点）")]
        [Tooltip("左手初始抓住的抓点物体名")]
        public string leftHandStartAnchorName = "ScatterAnchor_001";

        [Tooltip("右手初始抓住的抓点物体名")]
        public string rightHandStartAnchorName = "ScatterAnchor_002";

        [Tooltip("是否启用攀爬相机（越肩视角 + 二次 lookat）")]
        public bool cameraFollow = true;

        [Tooltip("相机初始越肩偏移（相对角色头部：x 肩侧、y 上、z 后）")]
        public Vector3 overShoulderOffset = new Vector3(0.6f, 0.55f, -3.2f);

        [Tooltip("松手吸附判定的查询半径（传给 SystemValidation 判定接口）")]
        public float gripQueryRadius = 0.5f;

        [Header("贴墙（3D 起伏岩壁）")]
        [Tooltip("是否让攀爬手沿 Z 轴射线贴合墙面表面")]
        public bool stickHandToWall = true;

        [Tooltip("手贴合墙面时略微前置的距离（朝相机方向，避免陷入墙体）")]
        public float wallSurfaceOffset = 0.05f;

        [Tooltip("贴墙射线的最大长度")]
        public float wallProbeDistance = 8f;

        [Tooltip("坠落判定用的 ForceSystem 配置（留空则用默认阈值）")]
        public ForceSystemConfig forceConfig;

        [Header("角色外观")]
        public Color bodyColor = new Color(0.2f, 0.5f, 0.85f);
        public Color handColor = new Color(0.95f, 0.8f, 0.65f);

        private const int LayerIgnoreRaycast = 2;
        private const int LayerUI = 5;

        private ClimbController3D _controller;

        public ClimbController3D Controller => _controller;
        public IClimbStateSource StateSource => _controller;

        private IEnumerator Start()
        {
            // 关卡（RouteNetwork/LevelMgr）可能在自身 Start 里运行时生成散布抓点；
            // 等待若干帧直到目标抓点出现，避免采集时抓点尚未生成而退回默认起攀点。
            for (int i = 0; i < 10; i++)
            {
                bool ready = GameObject.Find(routeRootName) != null &&
                             (string.IsNullOrEmpty(primaryStartAnchorName) || GameObject.Find(primaryStartAnchorName) != null) &&
                             (string.IsNullOrEmpty(leftHandStartAnchorName) || GameObject.Find(leftHandStartAnchorName) != null) &&
                             (string.IsNullOrEmpty(rightHandStartAnchorName) || GameObject.Find(rightHandStartAnchorName) != null);
                if (ready) break;
                yield return null;
            }

            EnsureConfigs();

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[Climb3CLevelBinder] 场景中未找到 MainCamera，无法绑定攀爬 3C。");
                yield break;
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
                yield break;
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

            RivetPoint leftStart = ResolveStartAnchor(rivetField, leftHandStartAnchorName);
            RivetPoint rightStart = ResolveStartAnchor(rivetField, rightHandStartAnchorName);
            if (useNearestStartAnchorPair && !string.IsNullOrEmpty(primaryStartAnchorName))
            {
                if (TryResolveNearestStartPair(rivetField, primaryStartAnchorName, out var resolvedLeft, out var resolvedRight))
                {
                    leftStart = resolvedLeft;
                    rightStart = resolvedRight;
                }
                else
                {
                    Debug.LogWarning($"[Climb3CLevelBinder] 未能从主抓点 '{primaryStartAnchorName}' 推导初始双手抓点，改用显式配置或默认起攀点。");
                }
            }

            Vector3 startCenter;
            if (useConfiguredStartCenter)
            {
                startCenter = configuredStartCenter;
                startCenter.z = planeZ - characterFrontOffset;
            }
            else if (leftStart != null && rightStart != null)
            {
                Vector3 mid = (leftStart.GrabPosition + rightStart.GrabPosition) * 0.5f;
                startCenter = new Vector3(mid.x, mid.y + tuning.torsoCenterOffset.y, planeZ - characterFrontOffset);
            }
            else
            {
                if (!string.IsNullOrEmpty(leftHandStartAnchorName) || !string.IsNullOrEmpty(rightHandStartAnchorName))
                {
                    Debug.LogWarning($"[Climb3CLevelBinder] 未找到初始抓点 '{leftHandStartAnchorName}' / '{rightHandStartAnchorName}'，改用默认起攀点。");
                }
                startCenter = new Vector3(centerX, min.y - startBelowLowest, planeZ - characterFrontOffset);
            }

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

            // 墙面深度探针：沿 +Z 打射线让手贴合起伏墙面（排除角色/UI 层，命中墙体/岩壁/抓点）
            WallDepthProbe wallProbe = null;
            if (stickHandToWall)
            {
                int wallMask = ~((1 << LayerIgnoreRaycast) | (1 << LayerUI));
                wallProbe = new WallDepthProbe(wallMask, planeZ - wallProbeDistance * 0.5f, wallProbeDistance, wallSurfaceOffset);
            }

            var gameContext = new GameContext(0) { GripQueryRadius = gripQueryRadius };

            // SystemValidation 抓握判定提供者：从场景所有 AnchorPoint（ScatterAnchors）构建
            var anchorRegistryGo = new GameObject("Climb3C_AnchorRegistry");
            var anchorRegistry = anchorRegistryGo.AddComponent<LevelAnchorRegistry>();
            anchorRegistry.RebuildFromScene();

            // --- 相机：越肩基座 + 二次 lookat ---
            ClimbCamera climbCam = null;
            if (cameraFollow)
            {
                climbCam = cam.GetComponent<ClimbCamera>();
                if (climbCam == null) climbCam = cam.gameObject.AddComponent<ClimbCamera>();
                cameraConfig.overShoulderOffset = overShoulderOffset;
                climbCam.Configure(cameraConfig, character);
            }

            // --- 控制器 ---
            var controllerGo = new GameObject("Climb3C_Controller");
            _controller = controllerGo.AddComponent<ClimbController3D>();
            _controller.Initialize(gameContext, 0, tuning, armRig, stamina, haptic, character, input,
                projector, rivetField, haptics, magnifierComp, staminaBar, startCenter);
            _controller.SetFallDependencies(ragdollFall, climbCam);
            _controller.SetZoneOverlay(zoneOverlay);
            _controller.SetGripProvider(anchorRegistry);
            _controller.SetWallProbe(wallProbe);
            _controller.SetCameraConfig(cameraConfig);
            if (forceConfig != null) _controller.SetForceSettings(forceConfig.Settings);

            // 让角色一开始就双手抓在指定抓点上
            if (leftStart != null && rightStart != null)
            {
                _controller.SetInitialGrips(leftStart, rightStart);
            }

            Debug.Log($"[Climb3CLevelBinder] 攀爬 3C 已绑定到关卡：抓点 {nodes.Count} 个，起攀点 {startCenter}。");
        }

        private static RivetPoint FindRivetByName(RivetField field, string objectName)
        {
            if (field == null || string.IsNullOrEmpty(objectName)) return null;
            var rivets = field.Rivets;
            for (int i = 0; i < rivets.Count; i++)
            {
                if (rivets[i] != null && rivets[i].name == objectName) return rivets[i];
            }
            return null;
        }

        /// <summary>
        /// 解析初始抓点：先在已注册铆钉里按名字找；找不到则按名字在整个场景查找
        /// （兼容抓点带子物体、非叶节点等情况），必要时补挂 RivetPoint 并注册。
        /// </summary>
        private static RivetPoint ResolveStartAnchor(RivetField field, string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return null;

            var byField = FindRivetByName(field, objectName);
            if (byField != null) return byField;

            var go = GameObject.Find(objectName);
            if (go == null) return null;

            var rivet = go.GetComponent<RivetPoint>();
            if (rivet == null) rivet = go.AddComponent<RivetPoint>();
            field.Register(rivet);
            return rivet;
        }

        private static bool TryResolveNearestStartPair(RivetField field, string primaryName, out RivetPoint left, out RivetPoint right)
        {
            left = null;
            right = null;
            var primary = ResolveStartAnchor(field, primaryName);
            if (primary == null) return false;

            var neighbor = FindNearestRivet(field, primary);
            if (neighbor == null) return false;

            if (primary.GrabPosition.x <= neighbor.GrabPosition.x)
            {
                left = primary;
                right = neighbor;
            }
            else
            {
                left = neighbor;
                right = primary;
            }

            return true;
        }

        private static RivetPoint FindNearestRivet(RivetField field, RivetPoint origin)
        {
            if (field == null || origin == null) return null;

            var rivets = field.Rivets;
            RivetPoint best = null;
            var bestSqr = float.PositiveInfinity;
            var originPosition = origin.GrabPosition;
            for (int i = 0; i < rivets.Count; i++)
            {
                var candidate = rivets[i];
                if (candidate == null || candidate == origin) continue;

                var sqr = (candidate.GrabPosition - originPosition).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = candidate;
            }

            return best;
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
