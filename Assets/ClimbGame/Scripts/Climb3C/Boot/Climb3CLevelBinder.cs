using System.Collections;
using System.Collections.Generic;
using Anchor.ForceSystem;
using Anchor.LevelAnchorSystem;
using Anchor.RivetRopeSystem;
using Anchor.SystemValidation;
using ClimbGame.Climb3C.Character;
using ClimbGame.Climb3C.Config;
using ClimbGame.Climb3C.Core;
using ClimbGame.Climb3C.Feedback;
using ClimbGame.Climb3C.Gameplay;
using ClimbGame.Climb3C.Input;
using ClimbGame.Climb3C.State;
using ClimbGame.Climb3C.UI;
using DesignerSpace;
using FIMSpace.FProceduralAnimation;
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

        [Tooltip("网络/配置指定的起攀点物体名；填写后优先使用该 Transform 位置作为起攀中心")]
        public string configuredStartPointName;

        [Tooltip("网络/配置指定的起攀主抓点名；启用后会自动寻找最近邻抓点组成左右手初始抓握")]
        public string primaryStartAnchorName;

        [Tooltip("根据起攀主抓点自动选择最近邻抓点作为另一只手")]
        public bool useNearestStartAnchorPair;

        [Header("初始双手抓点（按场景物体名查找；留空则用默认起攀点）")]
        [Tooltip("左手初始抓住的抓点物体名")]
        public string leftHandStartAnchorName = "ScatterAnchor_001";

        [Tooltip("右手初始抓住的抓点物体名")]
        public string rightHandStartAnchorName = "ScatterAnchor_002";

        [Tooltip("是否启用旧的 ClimbCamera 越肩跟随相机。若场景由 CameraMgr（cam0/1/2 机位）接管相机，请保持关闭，避免两者同帧抢写 Main Camera 导致抖动")]
        public bool cameraFollow = false;

        [Header("绳索力反馈（默认关闭，先在绳索测试关卡验收）")]
        [Tooltip("测试关卡验收通过后再开启：把 RivetRopeDebugDriver 输出的绳索力反馈交给 3C 消费")]
        public bool enableRopeForceFeedback = false;

        [Tooltip("提供绳索力反馈结果的调试驱动；留空时运行时尝试查找场景中的 RivetRopeDebugDriver")]
        public RivetRopeDebugDriver ropeDebugDriver;

        [Tooltip("相机初始越肩偏移（相对角色头部：x 肩侧、y 上、z 后）")]
        public Vector3 overShoulderOffset = new Vector3(0.6f, 0.55f, -3.2f);

        [Tooltip("松手吸附判定的查询半径（传给 SystemValidation 判定接口）")]
        public float gripQueryRadius = 0.5f;

        [Header("贴墙（3D 起伏岩壁）")]
        [Tooltip("是否让攀爬手沿 Z 轴射线贴合墙面表面")]
        public bool stickHandToWall = true;

        [Tooltip("手贴合墙面时略微前置的距离（朝相机方向，避免陷入墙体）")]
        public float wallSurfaceOffset = 0.05f;

        [Tooltip("贴墙射线起点在抓点平面前方的距离（需大于岩石体的前后厚度，确保从岩石外部发射）")]
        public float wallProbeFrontStart = 25f;

        [Tooltip("贴墙射线越过抓点平面继续延伸的长度")]
        public float wallProbeDistance = 8f;

        [Tooltip("坠落判定用的 ForceSystem 配置（留空则用默认阈值）")]
        public ForceSystemConfig forceConfig;

        [Header("角色（直接使用场景中已放置的角色）")]
        [Tooltip("场景中角色物体名（带 RagdollAnimator2）")]
        public string sceneCharacterName = "RagDollMan";

        [Tooltip("正式角色 Prefab；留空且场景中找不到 sceneCharacterName 时按下面路径实例化")]
        public GameObject characterPrefab;

        [Tooltip("角色 Prefab 资源路径（编辑器下回退实例化用）")]
        public string characterPrefabPath = "Assets/Thridpart/PolyOne/FreeStickman/RagDollMan/PR_RagdollDemo_Mannequin.prefab";

        [Tooltip("角色缩放")]
        public float characterScale = 1f;

        [Tooltip("ragdoll 生效前的初始旋转（欧拉角）：把角色摆成平行于水平面/躺平，适配不同关卡攀爬面朝向")]
        public Vector3 initialFlatRotationEuler = new Vector3(90f, 0f, 0f);

        [Tooltip("是否用下面的绝对坐标覆盖角色初始位置（关闭则用抓点推导出的起攀中心）")]
        public bool overrideCharacterInitialPosition = false;

        [Tooltip("角色初始位置（世界坐标）；overrideCharacterInitialPosition 开启时生效")]
        public Vector3 characterInitialPosition = Vector3.zero;

        [Tooltip("在最终初始位置上再叠加的偏移量（覆盖与否都生效），便于微调")]
        public Vector3 initialPositionOffset = Vector3.zero;

        [Header("Magnet Point 攀爬")]
        [Tooltip("两手磁点允许的最大间距（米），攻击手超出此范围会被夹取到边界")]
        public float maxHandDistance = 2f;

        [Tooltip("touch 目标位与磁点真实位置的差值超过该值（米）时，取消本次 touch")]
        public float handSlipCancelDistance = 0.5f;

        [Tooltip("手吸附在铆钉上时，磁点沿 -Z 方向偏移的距离（米）")]
        public float gripMagnetZOffset = 0.1f;

        [Tooltip("松手判定吸附：移动手与最近吸附点（ScatterAnchor）的 xy 投影距离小于该值即吸附成功（米）")]
        public float grabSnapDistanceXY = 0.5f;

        [Tooltip("放大镜总开关（默认关闭；开启后显示攀爬手的 RT 放大镜）")]
        public bool enableMagnifier = false;

        [Header("角色配色（用于联机时区分本地/远端玩家）")]
        [Tooltip("躯干配色（作为色调叠加到角色渲染器上）")]
        public Color bodyColor = new Color(0.2f, 0.5f, 0.85f);

        [Tooltip("手部配色（作用于手部骨骼下的渲染器，若无独立手部渲染器则忽略）")]
        public Color handColor = new Color(0.95f, 0.8f, 0.65f);

        [Header("防穿模胶囊体")]
        public Vector3 capsuleCenter = new Vector3(0f, 0f, 0f);
        public float capsuleHeight = 1.6f;
        public float capsuleRadius = 0.3f;

        [Tooltip("身体保持在墙面前方的距离（应大于胶囊半径，避免身体陷入墙体）")]
        public float bodyWallOffset = 0.4f;

        [Tooltip("是否启用胶囊体 vs 墙体的去穿模（沿碰撞法线把角色推出贴合墙面）")]
        public bool resolveCapsulePenetration = true;

        [Tooltip("参与去穿模检测的墙体层（默认排除角色所在的 Ignore Raycast 层与 UI 层）")]
        public LayerMask wallCollisionMask = ~((1 << 2) | (1 << 5));

        [Tooltip("去穿模迭代次数：贴多面墙的角落需要多次迭代才能同时推出")]
        public int depenetrationIterations = 4;

        private const int LayerIgnoreRaycast = 2;
        private const int LayerUI = 5;

        private ClimbController3D _controller;

        public ClimbController3D Controller => _controller;
        public IClimbStateSource StateSource => _controller;

        private void Update()
        {
            if (_controller == null)
            {
                return;
            }

            if (!enableRopeForceFeedback)
            {
                _controller.SetRopeForceFeedbackEnabled(false);
                return;
            }

            _controller.SetRopeForceFeedbackEnabled(true);

            if (ropeDebugDriver == null)
            {
                ropeDebugDriver = FindObjectOfType<RivetRopeDebugDriver>();
            }

            if (ropeDebugDriver == null)
            {
                _controller.ClearRopeForceFeedback("NoRopeDriver");
                return;
            }

            _controller.ConsumeRopeForceFeedback(ropeDebugDriver.LastForceFeedback);
        }

        private IEnumerator Start()
        {
            // 时序：Build 需要等抓点就绪（可能延后若干帧），期间角色会停在场景原始位置。
            // 为保证"第一帧"角色就位于配置的初始位置/旋转，这里在等待抓点之前先摆一次。
            ApplyInitialPlacement();

            // 关卡（RouteNetwork/LevelMgr）可能在自身 Start 里运行时生成散布抓点；
            // 等待若干帧直到目标抓点出现，避免采集时抓点尚未生成而退回默认起攀点。
            for (int i = 0; i < 10; i++)
            {
                bool ready = GameObject.Find(routeRootName) != null &&
                             (string.IsNullOrEmpty(configuredStartPointName) || GameObject.Find(configuredStartPointName) != null) &&
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
                startCenter = ResolveConfiguredStartCenter();
                if (!HasConfiguredStartPoint())
                {
                    startCenter.z = planeZ - characterFrontOffset;
                }
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

            // --- 角色：直接使用场景中已放置的角色（RagDollMan），全程 RA2 全布娃娃 + 双手磁点 ---
            GameObject sceneCharacter = string.IsNullOrEmpty(sceneCharacterName) ? null : GameObject.Find(sceneCharacterName);
            if (sceneCharacter == null)
            {
                // 回退：场景里没有则实例化 Prefab（保证在纯净场景下也能跑）
                GameObject prefab = characterPrefab;
#if UNITY_EDITOR
                if (prefab == null && !string.IsNullOrEmpty(characterPrefabPath))
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(characterPrefabPath);
                }
#endif
                if (prefab == null)
                {
                    Debug.LogError($"[Climb3CLevelBinder] 场景中未找到角色 '{sceneCharacterName}'，且未指定回退 Prefab。");
                    yield break;
                }
                sceneCharacter = Instantiate(prefab);
                sceneCharacter.name = string.IsNullOrEmpty(sceneCharacterName) ? prefab.name : sceneCharacterName;
                if (characterScale != 1f) sceneCharacter.transform.localScale = Vector3.one * characterScale;
            }

            var avatar = new MagnetClimberAvatar(sceneCharacter, armRig, ragdollFall, initialFlatRotationEuler, characterScale);
            // 角色初始摆放位置：默认用抓点推导的起攀中心，可用绝对坐标覆盖，并再叠加偏移微调。
            // 手部仍锚定到配置抓点（下面 SetInitialGrips），磁点会把双手拉到抓点上。
            Vector3 characterPosition = (overrideCharacterInitialPosition ? characterInitialPosition : startCenter) + initialPositionOffset;
            avatar.Build(null, characterPosition, null, null);
            ApplyAvatarTint(avatar.Root, bodyColor, handColor);
            SetLayerRecursive(avatar.Root, LayerIgnoreRaycast);

            // --- Canvas / UI ---
            var canvasGo = new GameObject("Climb3C_Canvas");
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

            // 墙面深度探针：沿 +Z 打射线，仅修正手的 z 贴合起伏墙面（排除角色/UI 层）
            WallDepthProbe wallProbe = null;
            if (stickHandToWall)
            {
                int wallMask = ~((1 << LayerIgnoreRaycast) | (1 << LayerUI));
                // 射线起点必须在所有岩石"前方"（远离墙面、靠近相机一侧），否则从岩石 MeshCollider 内部
                // 发射会打不到正面。从抓点平面前方 wallProbeFrontStart 处沿 +Z 打，命中岩石正面。
                wallProbe = new WallDepthProbe(wallMask, planeZ - wallProbeFrontStart, wallProbeFrontStart + wallProbeDistance, wallSurfaceOffset);
            }

            var gameContext = new GameContext(0) { GripQueryRadius = gripQueryRadius };

            // SystemValidation 抓握判定提供者：从场景所有 AnchorPoint（ScatterAnchors）构建
            var anchorRegistryGo = new GameObject("Climb3C_AnchorRegistry");
            var anchorRegistry = anchorRegistryGo.AddComponent<LevelAnchorRegistry>();
            anchorRegistry.RebuildFromScene();

            // --- 相机：越肩基座 + 二次 lookat ---
            // 若场景由 CameraMgr（cam0/1/2 机位）接管相机，则不启用旧的 ClimbCamera，
            // 避免两个脚本在 LateUpdate 同帧抢写 Main Camera 造成抖动。
            ClimbCamera climbCam = null;
            bool cameraMgrTakesOver = FindObjectOfType<CameraMgr>() != null;
            if (cameraMgrTakesOver)
            {
                // 主动禁用相机上可能残留的 ClimbCamera，确保相机唯一控制者是 CameraMgr。
                var existingClimbCam = cam.GetComponent<ClimbCamera>();
                if (existingClimbCam != null) existingClimbCam.enabled = false;
            }
            else if (cameraFollow)
            {
                climbCam = cam.GetComponent<ClimbCamera>();
                if (climbCam == null) climbCam = cam.gameObject.AddComponent<ClimbCamera>();
                cameraConfig.overShoulderOffset = overShoulderOffset;
                climbCam.Configure(cameraConfig, avatar);
            }

            // --- 控制器 ---
            var controllerGo = new GameObject("Climb3C_Controller");
            _controller = controllerGo.AddComponent<ClimbController3D>();
            _controller.Initialize(gameContext, 0, tuning, armRig, stamina, haptic, avatar, input,
                projector, rivetField, haptics, magnifierComp, staminaBar, startCenter);
            _controller.SetFallDependencies(ragdollFall, climbCam);
            _controller.SetGripProvider(anchorRegistry);
            _controller.SetWallProbe(wallProbe);
            _controller.SetBodyWallOffset(bodyWallOffset);
            _controller.SetMaxHandDistance(maxHandDistance);
            _controller.SetHandSlipCancelDistance(handSlipCancelDistance);
            _controller.SetGripMagnetZOffset(gripMagnetZOffset);
            _controller.SetGrabSnapDistanceXY(grabSnapDistanceXY);
            _controller.SetMagnifierEnabled(enableMagnifier);
            _controller.SetRopeForceFeedbackEnabled(enableRopeForceFeedback);

            // 胶囊体防穿模：把角色胶囊体与墙体做重叠检测，穿插时沿法线推出贴合墙面
            if (resolveCapsulePenetration && avatar.BodyCapsule != null)
            {
                var wallResolver = new CapsuleWallResolver(avatar.BodyCapsule, wallCollisionMask, depenetrationIterations);
                _controller.SetWallResolver(wallResolver);
            }
            _controller.SetCameraConfig(cameraConfig);
            if (forceConfig != null) _controller.SetForceSettings(forceConfig.Settings);

            var debugGo = new GameObject("Climb3C_InputDebug");
            debugGo.transform.SetParent(servicesGo.transform, false);
            var debugOverlay = debugGo.AddComponent<Climb3CInputDebugOverlay>();
            debugOverlay.Build(canvas, tuning, cam, _controller);

            // 启动时角色 ragdoll 保持停止（Build 已切站立/运动学）。
            // 第二帧：仍在站立模式下设置玩家初始位置/旋转/缩放（ragdoll 仍停止）。
            yield return null;
            avatar.SetInitialTransform(characterPosition);

            // 第三帧：开放 ragdoll——切全布娃娃并挂磁点到抓点。
            yield return null;
            Vector3 leftHold = leftStart != null
                ? leftStart.GrabPosition
                : characterPosition + new Vector3(-0.3f, 0.3f, 0f);
            Vector3 rightHold = rightStart != null
                ? rightStart.GrabPosition
                : characterPosition + new Vector3(0.3f, 0.3f, 0f);
            avatar.CommitClimbRagdoll(leftHold, rightHold);

            // 让角色一开始就双手抓在指定抓点上
            if (leftStart != null && rightStart != null)
            {
                _controller.SetInitialGrips(leftStart, rightStart);
            }

            // 磁点创建后，RA2 的 dummy 需要若干帧才初始化完成；延迟 3 帧再用 TranslateTo
            // 把布娃娃整套骨骼移动到配置的初始位置，确保生效。
            for (int f = 0; f < 3; f++) yield return null;
            avatar.SetRagdollPosition(characterPosition);

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

        /// <summary>
        /// 第一帧先把场景角色摆到配置的初始旋转（始终）与初始位置（启用绝对覆盖时）。
        /// 关键：RA2 在 Falling 模式下 root.transform 不驱动可见姿态，必须先切 Standing 模式，
        /// root 的位置/旋转才会生效；随后 avatar.Build 会保持站立摆放，第二帧再切 Fall。
        /// </summary>
        private void ApplyInitialPlacement()
        {
            GameObject go = string.IsNullOrEmpty(sceneCharacterName) ? null : GameObject.Find(sceneCharacterName);
            if (go == null) return;

            // Animator 始终禁用（不需要任何动画）。
            var animator = go.GetComponentInChildren<Animator>();
            if (animator != null) animator.enabled = false;
            // 初始停止 ragdoll：直接禁用 RagdollAnimator2 组件，此时角色为静态骨骼、root.transform 生效。
            var ra2 = go.GetComponent<RagdollAnimator2>();
            if (ra2 != null) ra2.enabled = false;

            go.transform.rotation = Quaternion.Euler(initialFlatRotationEuler);
            if (overrideCharacterInitialPosition)
            {
                go.transform.position = characterInitialPosition + initialPositionOffset;
            }
            else if (useConfiguredStartCenter)
            {
                go.transform.position = ResolveConfiguredStartCenter() + initialPositionOffset;
            }
        }

        private Vector3 ResolveConfiguredStartCenter()
        {
            if (!string.IsNullOrEmpty(configuredStartPointName))
            {
                var startPoint = GameObject.Find(configuredStartPointName);
                if (startPoint != null)
                {
                    return startPoint.transform.position;
                }
            }

            return configuredStartCenter;
        }

        private bool HasConfiguredStartPoint()
        {
            return !string.IsNullOrEmpty(configuredStartPointName) &&
                   GameObject.Find(configuredStartPointName) != null;
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

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>
        /// 用色调叠加区分不同玩家：整体染 bodyColor，手部骨骼（LeftHand/RightHand）下若有独立渲染器则染 handColor。
        /// 通过 MaterialPropertyBlock 覆盖颜色，不改动共享材质，避免影响其它角色实例。
        /// </summary>
        private static void ApplyAvatarTint(Transform root, Color bodyColor, Color handColor)
        {
            if (root == null) return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                Color tint = IsUnderHand(renderer.transform, root) ? handColor : bodyColor;
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, tint);
                block.SetColor(ColorId, tint);
                renderer.SetPropertyBlock(block);
            }
        }

        private static bool IsUnderHand(Transform node, Transform root)
        {
            for (Transform t = node; t != null && t != root.parent; t = t.parent)
            {
                if (t.name == "LeftHand" || t.name == "RightHand") return true;
            }
            return false;
        }
    }
}
