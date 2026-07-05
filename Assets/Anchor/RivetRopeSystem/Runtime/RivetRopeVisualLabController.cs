using UnityEngine;

namespace Anchor.RivetRopeSystem
{
    public sealed class RivetRopeVisualLabController : MonoBehaviour
    {
        private enum LabPathPreset
        {
            Direct = 0,
            SingleRivet = 1,
            MultiRivet = 2,
            SharpTurn = 3
        }

        [Header("References")]
        [SerializeField] private RivetRopeConfig config;
        [SerializeField] private RivetRopeDebugDriver driver;
        [SerializeField] private RivetRopeLineVisual visual;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform lowerEndpoint;
        [SerializeField] private Transform upperEndpoint;
        [SerializeField] private Transform[] rivetMarkers;
        [SerializeField] private Transform feedbackProxy;
        [SerializeField] private Transform characterRoot;
        [SerializeField] private Transform forceTargetBone;

        [Header("Lab Motion")]
        [SerializeField] private bool autoMoveUpper = true;
        [SerializeField] private float autoMoveAmplitude = 1.6f;
        [SerializeField] private float autoMoveSpeed = 0.65f;
        [SerializeField] private float nudgeStep = 0.35f;
        [SerializeField] private float dragPickRadius = 0.45f;
        [SerializeField] private bool enableForceFeedbackPreview = true;
        [SerializeField] private bool feedbackDrivesUpperEndpoint;
        [SerializeField] private bool useCharacterForceTarget = true;
        [SerializeField] private float characterFeedbackFollow = 10f;
        [SerializeField] private bool simulateFallCatch;
        [SerializeField] private float fallGravity = 2f;
        [SerializeField] private float fallInitialDownSpeed;
        [SerializeField] private float maxFallSpeed = 6f;
        [SerializeField] private float fallCatchImpulse = 0.65f;
        [SerializeField] private float fallCatchSpring = 7.5f;
        [SerializeField] private float fallCatchDamping = 1.8f;
        [SerializeField] private float fallCatchImpulseStretch = 0.28f;
        [SerializeField] private bool loopFallCatchPreview = true;
        [SerializeField] private float fallReplayDelay = 1.4f;

        [Header("Lab UI")]
        [SerializeField] private Rect panelRect = new Rect(16f, 16f, 460f, 680f);
        [SerializeField] private LabPathPreset preset = LabPathPreset.SingleRivet;

        private RivetRopeSettings _settings;
        private RivetRopeVisualSettings _visualSettings;
        private RopeForceFeedbackResult _feedback;
        private Transform _dragTarget;
        private Vector3 _dragLastWorld;
        private Vector3 _upperBasePosition;
        private Vector3 _lastUpperPosition;
        private Vector3 _fallVelocity;
        private bool _hasLastUpperPosition;
        private bool _fallWasCaught;
        private bool _fallReboundTriggered;
        private float _fallCaughtAt;

        public static string DebugRunFirstFallCatchTest()
        {
            var lab = FindObjectOfType<RivetRopeVisualLabController>();
            if (lab == null)
            {
                const string missing = "Rope force fall catch: no RivetRopeVisualLabController found";
                Debug.LogError(missing);
                return missing;
            }

            lab.DebugPrepareFallCatchTest();
            lab.DebugStartFallCatchTest();
            const string started = "Rope force fall catch started";
            Debug.Log(started, lab);
            return started;
        }

        private void Start()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            _settings = config != null ? config.Settings : RivetRopeSettings.CreateDefault();
            _visualSettings = config != null ? config.VisualSettings : RivetRopeVisualSettings.CreateDefault();
            ResolveForceTargetBone();
            if (upperEndpoint != null)
            {
                _upperBasePosition = upperEndpoint.position;
            }

            ApplyRuntimeSettings(true);
            EnsureFeedbackProxy();
            ResetFeedbackProxy();
            ApplyPreset();
        }

        private void Update()
        {
            UpdateAutoMotion();
            UpdateEndpointDrag();
            UpdateFallCatchSimulation();
            UpdateForceFeedbackPreview();
        }

        private void OnGUI()
        {
            if (driver == null || visual == null)
            {
                return;
            }

            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label("Rivet Rope Visual Lab");
            GUILayout.Label($"Mode: {_visualSettings.VisualMode}");
            GUILayout.Label($"Rope: {driver.LastPath.TensionState} used={driver.LastPath.UsedLength:0.00} slack={driver.LastPath.RemainingSlack:0.00} constraint={driver.LastPath.ConstraintDistance:0.00}");
            GUILayout.Label($"Render: points={visual.RenderPointCount} length={visual.LastRenderedLength:0.00}");
            GUILayout.Label($"Force: active={_feedback.IsActive} reason={_feedback.Reason} strength={_feedback.TensionStrength:0.00} constraint={_feedback.ConstraintDistance:0.00}");
            GUILayout.Label($"Force Dir: {_feedback.TensionDirection.x:0.00}, {_feedback.TensionDirection.y:0.00}, {_feedback.TensionDirection.z:0.00}");
            GUILayout.Label($"Velocity Fix: {_feedback.SuggestedVelocityCorrection.magnitude:0.00} m/s");
            GUILayout.Label($"Character Bone: {(forceTargetBone != null ? forceTargetBone.name : "None")}");
            GUILayout.Label($"Fall Test: active={simulateFallCatch} velocity={_fallVelocity.magnitude:0.00}");

            GUILayout.Space(6f);
            GUILayout.Label("Path Presets");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("直连")) SetPreset(LabPathPreset.Direct);
            if (GUILayout.Button("单锚")) SetPreset(LabPathPreset.SingleRivet);
            if (GUILayout.Button("多锚")) SetPreset(LabPathPreset.MultiRivet);
            if (GUILayout.Button("急转弯")) SetPreset(LabPathPreset.SharpTurn);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            GUILayout.Label("Endpoint Follow");
            autoMoveUpper = GUILayout.Toggle(autoMoveUpper, "自动移动上方端点");
            enableForceFeedbackPreview = GUILayout.Toggle(enableForceFeedbackPreview, "启用绳索力反馈预览");
            feedbackDrivesUpperEndpoint = GUILayout.Toggle(feedbackDrivesUpperEndpoint, "反馈驱动角色/上方端点（实验）");
            useCharacterForceTarget = GUILayout.Toggle(useCharacterForceTarget, "用角色躯干骨骼作为受力点");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("上移")) NudgeUpper(Vector3.up);
            if (GUILayout.Button("下移")) NudgeUpper(Vector3.down);
            if (GUILayout.Button("左移")) NudgeUpper(Vector3.left);
            if (GUILayout.Button("右移")) NudgeUpper(Vector3.right);
            GUILayout.EndHorizontal();
            GUILayout.Label("也可以用鼠标拖动上下端点球体");

            GUILayout.Space(6f);
            GUILayout.Label("Fall Catch Test");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("准备下坠测试")) PrepareFallCatchTest();
            if (GUILayout.Button(simulateFallCatch ? "停止下坠" : "开始下坠")) ToggleFallCatch();
            GUILayout.EndHorizontal();
            loopFallCatchPreview = GUILayout.Toggle(loopFallCatchPreview, "循环播放下坠/拉住");
            GUILayout.Label("青色点=保护点，角色从上方下坠，绳长耗尽后 Spine2 被拉住。");
            GUILayout.Label($"Catch: caught={_fallWasCaught} rebound={_fallReboundTriggered} impulse={fallCatchImpulse:0.00} replay={fallReplayDelay:0.00}s");

            GUILayout.Space(6f);
            GUILayout.Label("Visual Mode");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("程序化线条")) SetMode(RivetRopeVisualMode.ProceduralLine);
            if (GUILayout.Button("Verlet 绳段")) SetMode(RivetRopeVisualMode.VerletSegments);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            DrawSlider("绳长", ref _settings.TotalRopeLength, 2f, 12f, true);
            DrawSlider("预拉紧阈值", ref _settings.ForcePreTensionThreshold, 0f, 3f, true);
            DrawSlider("最大修正", ref _settings.ForceMaxConstraintCorrection, 0f, 1.5f, true);
            DrawSlider("弹性余量", ref _settings.ForceElasticStretch, 0f, 1.2f, true);
            DrawSlider("张力/米", ref _settings.ForceTensionStrengthPerMeter, 0f, 8f, true);
            DrawSlider("速度阻尼", ref _settings.ForceVelocityDamping, 0f, 2.5f, true);
            DrawSlider("轻回弹", ref _settings.ForceReboundStrength, 0f, 1.5f, true);
            DrawSlider("底部弹簧", ref fallCatchSpring, 0f, 16f, false);
            DrawSlider("底部阻尼", ref fallCatchDamping, 0f, 6f, false);
            DrawSlider("宽度", ref _visualSettings.Width, 0.01f, 0.18f, false);
            DrawSlider("下垂/米", ref _visualSettings.SlackSagPerMeter, 0f, 0.22f, false);
            DrawSlider("最大下垂", ref _visualSettings.MaxSag, 0f, 2.4f, false);
            DrawSlider("阻尼", ref _visualSettings.PhysicsDamping, 0.75f, 0.995f, false);
            DrawSlider("跟随", ref _visualSettings.TargetFollow, 0f, 16f, false);
            DrawSlider("摆动", ref _visualSettings.SwayAmplitude, 0f, 0.25f, false);
            DrawSlider("纹理平铺/米", ref _visualSettings.TextureTilesPerMeter, 0f, 4f, false);

            if (GUILayout.Button("重置路径和铆钉"))
            {
                ApplyRuntimeSettings(true);
                ResetFeedbackProxy();
                simulateFallCatch = false;
                _fallVelocity = Vector3.zero;
                ApplyPreset();
            }

            GUILayout.EndArea();
        }

        private void DrawSlider(string label, ref float value, float min, float max, bool resetModel)
        {
            GUILayout.Label($"{label}: {value:0.00}");
            var next = GUILayout.HorizontalSlider(value, min, max);
            if (Mathf.Abs(next - value) > 0.001f)
            {
                value = next;
                ApplyRuntimeSettings(resetModel);
                if (resetModel)
                {
                    ApplyPreset();
                }
            }
        }

        private void ApplyRuntimeSettings(bool resetModel)
        {
            _settings = _settings.Sanitized();
            _visualSettings = _visualSettings.Sanitized();
            if (config != null)
            {
                config.ConfigureRuntime(_settings);
                config.ConfigureRuntimeVisuals(_visualSettings);
            }

            if (visual != null)
            {
                visual.SetRuntimeVisualSettings(_visualSettings);
            }

            if (resetModel && driver != null)
            {
                driver.ResetModel();
            }
        }

        private void ApplyPreset()
        {
            if (driver == null)
            {
                return;
            }

            driver.ResetModel();
            SetEndpointPositionsForPreset();
            ResetFeedbackProxy();
            if (rivetMarkers == null)
            {
                return;
            }

            var activeCount = GetActiveRivetCount();
            for (int i = 0; i < rivetMarkers.Length; i++)
            {
                var marker = rivetMarkers[i];
                if (marker == null)
                {
                    continue;
                }

                marker.gameObject.SetActive(i < activeCount);
                if (i < activeCount)
                {
                    driver.DebugPlaceLeadRivet(marker.position);
                }
            }
        }

        private void SetEndpointPositionsForPreset()
        {
            if (lowerEndpoint != null)
            {
                lowerEndpoint.position = new Vector3(-2.4f, -3.2f, 0f);
            }

            if (upperEndpoint == null)
            {
                return;
            }

            var desiredUpper = upperEndpoint.position;
            switch (preset)
            {
                case LabPathPreset.Direct:
                    desiredUpper = new Vector3(2.4f, 4.6f, 0f);
                    break;
                case LabPathPreset.SingleRivet:
                    desiredUpper = new Vector3(2.2f, 4.8f, 0f);
                    SetMarkerPosition(0, new Vector3(0f, 0.9f, 0f));
                    break;
                case LabPathPreset.MultiRivet:
                    desiredUpper = new Vector3(2.5f, 5.2f, 0f);
                    SetMarkerPosition(0, new Vector3(-1.2f, -0.6f, 0f));
                    SetMarkerPosition(1, new Vector3(0.9f, 1.5f, 0f));
                    SetMarkerPosition(2, new Vector3(-0.2f, 3.2f, 0f));
                    break;
                case LabPathPreset.SharpTurn:
                    desiredUpper = new Vector3(2.8f, 4.8f, 0f);
                    SetMarkerPosition(0, new Vector3(-1.8f, 0.4f, 0f));
                    SetMarkerPosition(1, new Vector3(1.8f, 1.2f, 0f));
                    SetMarkerPosition(2, new Vector3(-1.2f, 2.8f, 0f));
                    break;
            }

            MoveCharacterForceTargetTo(desiredUpper);
            _upperBasePosition = upperEndpoint.position;
        }

        private void SetMarkerPosition(int index, Vector3 position)
        {
            if (rivetMarkers != null && index >= 0 && index < rivetMarkers.Length && rivetMarkers[index] != null)
            {
                rivetMarkers[index].position = position;
            }
        }

        private int GetActiveRivetCount()
        {
            switch (preset)
            {
                case LabPathPreset.SingleRivet:
                    return 1;
                case LabPathPreset.MultiRivet:
                case LabPathPreset.SharpTurn:
                    return 3;
                default:
                    return 0;
            }
        }

        private void SetPreset(LabPathPreset nextPreset)
        {
            if (preset == nextPreset)
            {
                return;
            }

            preset = nextPreset;
            ApplyPreset();
        }

        private void SetMode(RivetRopeVisualMode mode)
        {
            if (_visualSettings.VisualMode == mode)
            {
                return;
            }

            _visualSettings.VisualMode = mode;
            ApplyRuntimeSettings(false);
        }

        private void NudgeUpper(Vector3 direction)
        {
            if (upperEndpoint == null)
            {
                return;
            }

            autoMoveUpper = false;
            MoveCharacterForceTargetTo(upperEndpoint.position + direction * nudgeStep);
            _upperBasePosition = upperEndpoint.position;
        }

        private void UpdateAutoMotion()
        {
            if (!autoMoveUpper || upperEndpoint == null)
            {
                return;
            }

            var offset = new Vector3(
                Mathf.Sin(Time.time * autoMoveSpeed) * autoMoveAmplitude,
                Mathf.Cos(Time.time * autoMoveSpeed * 0.7f) * autoMoveAmplitude * 0.35f,
                0f);
            MoveCharacterForceTargetTo(_upperBasePosition + offset);
        }

        private void UpdateEndpointDrag()
        {
            if (targetCamera == null)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                _dragTarget = PickEndpoint();
                if (_dragTarget != null)
                {
                    autoMoveUpper = false;
                    _dragLastWorld = MouseWorldPosition(_dragTarget.position.z);
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (_dragTarget == upperEndpoint && upperEndpoint != null)
                {
                    _upperBasePosition = upperEndpoint.position;
                }

                _dragTarget = null;
            }

            if (_dragTarget != null && Input.GetMouseButton(0))
            {
                var next = MouseWorldPosition(_dragTarget.position.z);
                if (_dragTarget == upperEndpoint && ShouldUseCharacterForceTarget())
                {
                    MoveCharacterForceTargetTo(upperEndpoint.position + (next - _dragLastWorld));
                    _dragLastWorld = next;
                }
                else
                {
                    _dragTarget.position = next;
                }
            }
        }

        private Transform PickEndpoint()
        {
            var mouse = MouseWorldPosition(0f);
            if (upperEndpoint != null && Vector3.Distance(mouse, upperEndpoint.position) <= dragPickRadius)
            {
                return upperEndpoint;
            }

            if (lowerEndpoint != null && Vector3.Distance(mouse, lowerEndpoint.position) <= dragPickRadius)
            {
                return lowerEndpoint;
            }

            return null;
        }

        private Vector3 MouseWorldPosition(float z)
        {
            var ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.forward, new Vector3(0f, 0f, z));
            return plane.Raycast(ray, out var enter) ? ray.GetPoint(enter) : Vector3.zero;
        }

        private void EnsureFeedbackProxy()
        {
            if (feedbackProxy != null)
            {
                return;
            }

            var proxy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proxy.name = "Rope Force Feedback Proxy";
            proxy.transform.SetParent(transform, true);
            proxy.transform.localScale = Vector3.one * 0.22f;
            var renderer = proxy.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = new Material(Shader.Find("Sprites/Default"));
                material.color = new Color(1f, 0.2f, 0.85f, 0.85f);
                renderer.sharedMaterial = material;
            }

            feedbackProxy = proxy.transform;
        }

        private void ResetFeedbackProxy()
        {
            if (feedbackProxy != null && upperEndpoint != null)
            {
                feedbackProxy.position = upperEndpoint.position;
            }

            _lastUpperPosition = upperEndpoint != null ? upperEndpoint.position : Vector3.zero;
            _hasLastUpperPosition = upperEndpoint != null;
            _feedback = default;
        }

        private void UpdateForceFeedbackPreview()
        {
            if (driver == null || lowerEndpoint == null || upperEndpoint == null)
            {
                return;
            }

            SyncUpperEndpointToForceBone();
            var velocity = CalculateUpperVelocity();
            var path = driver.Model.BuildRopePath(lowerEndpoint.position, upperEndpoint.position);
            _feedback = driver.Model.EvaluateForceFeedback("lab-upper", path, upperEndpoint.position, velocity, Time.deltaTime);

            if (!enableForceFeedbackPreview || !_feedback.IsActive)
            {
                if (feedbackProxy != null)
                {
                    feedbackProxy.position = Vector3.Lerp(feedbackProxy.position, upperEndpoint.position, 1f - Mathf.Exp(-8f * Time.deltaTime));
                }
                return;
            }

            var displacement = _feedback.SuggestedVelocityCorrection * Time.deltaTime;
            displacement = Vector3.ClampMagnitude(displacement, Mathf.Max(0.02f, _feedback.ConstraintDistance));

            if (feedbackProxy != null)
            {
                var target = upperEndpoint.position + displacement;
                feedbackProxy.position = Vector3.Lerp(feedbackProxy.position, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
            }

            if (feedbackDrivesUpperEndpoint)
            {
                if (simulateFallCatch)
                {
                    ApplyFeedbackToFallVelocity();
                }
                else
                {
                    ApplyFeedbackDisplacement(displacement);
                }

                _upperBasePosition = upperEndpoint.position;
            }
        }

        private void PrepareFallCatchTest()
        {
            simulateFallCatch = false;
            autoMoveUpper = false;
            enableForceFeedbackPreview = true;
            feedbackDrivesUpperEndpoint = true;
            useCharacterForceTarget = true;
            preset = LabPathPreset.Direct;
            fallGravity = 2f;
            fallInitialDownSpeed = 0f;
            maxFallSpeed = 6f;
            fallCatchImpulse = 0.65f;
            fallCatchSpring = 7.5f;
            fallCatchDamping = 1.8f;
            fallCatchImpulseStretch = 0.28f;
            fallReplayDelay = 1.4f;

            _settings.TotalRopeLength = 3.35f;
            _settings.ForcePreTensionThreshold = 0f;
            _settings.ForceMaxConstraintCorrection = 0.85f;
            _settings.ForceElasticStretch = 0.55f;
            _settings.ForceTensionStrengthPerMeter = 4.8f;
            _settings.ForceVelocityDamping = 0.75f;
            _settings.ForceReboundStrength = 0.48f;
            _settings.ForceMaxFeedbackStrength = 12f;
            ApplyRuntimeSettings(true);

            if (lowerEndpoint != null)
            {
                lowerEndpoint.position = new Vector3(0f, 2.2f, 0f);
            }

            MoveCharacterForceTargetTo(new Vector3(0f, 5.1f, 0f));
            _upperBasePosition = upperEndpoint != null ? upperEndpoint.position : Vector3.zero;
            _lastUpperPosition = _upperBasePosition;
            _hasLastUpperPosition = upperEndpoint != null;
            _fallVelocity = Vector3.down * fallInitialDownSpeed;
            _fallWasCaught = false;
            _fallReboundTriggered = false;
            _fallCaughtAt = 0f;

            if (driver != null)
            {
                driver.ResetModel();
            }
        }

        public void DebugPrepareFallCatchTest()
        {
            PrepareFallCatchTest();
        }

        public void DebugStartFallCatchTest()
        {
            if (!simulateFallCatch)
            {
                ToggleFallCatch();
            }
        }

        public void DebugStopFallCatchTest()
        {
            if (simulateFallCatch)
            {
                ToggleFallCatch();
            }
        }

        private void ToggleFallCatch()
        {
            if (!simulateFallCatch)
            {
                if (upperEndpoint == null || lowerEndpoint == null)
                {
                    return;
                }

                simulateFallCatch = true;
                autoMoveUpper = false;
                enableForceFeedbackPreview = true;
                feedbackDrivesUpperEndpoint = true;
                useCharacterForceTarget = true;
                _fallVelocity = Vector3.down * Mathf.Max(0f, fallInitialDownSpeed);
                _fallWasCaught = false;
                _fallReboundTriggered = false;
                _fallCaughtAt = 0f;
            }
            else
            {
                simulateFallCatch = false;
                _fallVelocity = Vector3.zero;
                _fallWasCaught = false;
                _fallReboundTriggered = false;
            }
        }

        private void UpdateFallCatchSimulation()
        {
            if (!simulateFallCatch || upperEndpoint == null)
            {
                return;
            }

            var dt = Mathf.Max(0f, Time.deltaTime);
            if (loopFallCatchPreview && _fallWasCaught && Time.time - _fallCaughtAt >= fallReplayDelay)
            {
                PrepareFallCatchTest();
                simulateFallCatch = true;
            }

            _fallVelocity += Vector3.down * Mathf.Max(0f, fallGravity) * dt;
            _fallVelocity = Vector3.ClampMagnitude(_fallVelocity, Mathf.Max(0.1f, maxFallSpeed));
            MoveCharacterForceTargetTo(upperEndpoint.position + _fallVelocity * dt);
        }

        private void ApplyFeedbackToFallVelocity()
        {
            if (!simulateFallCatch || !_feedback.IsActive)
            {
                return;
            }

            if (_feedback.Reason != RopeForceFeedbackReason.Taut)
            {
                return;
            }

            if (!_fallWasCaught)
            {
                _fallWasCaught = true;
                _fallCaughtAt = Time.time;
            }

            var dt = Mathf.Max(0.0001f, Time.deltaTime);
            var direction = _feedback.TensionDirection.sqrMagnitude > 0.000001f
                ? _feedback.TensionDirection.normalized
                : Vector3.up;
            var stretch = Mathf.Max(0f, _feedback.ConstraintDistance);
            var awaySpeed = Mathf.Max(0f, Vector3.Dot(_fallVelocity, -direction));

            _fallVelocity += direction * stretch * Mathf.Max(0f, fallCatchSpring) * dt;
            if (stretch > 0.02f && awaySpeed > 0f)
            {
                _fallVelocity += direction * awaySpeed * Mathf.Max(0f, fallCatchDamping) * dt;
            }

            if (!_fallReboundTriggered && stretch >= Mathf.Max(0.01f, fallCatchImpulseStretch))
            {
                _fallReboundTriggered = true;
                _fallVelocity += direction * Mathf.Max(0f, fallCatchImpulse);
            }

            _fallVelocity = Vector3.ClampMagnitude(_fallVelocity, Mathf.Max(0.1f, maxFallSpeed));
        }

        private bool ShouldUseCharacterForceTarget()
        {
            return useCharacterForceTarget && characterRoot != null && forceTargetBone != null;
        }

        private void ResolveForceTargetBone()
        {
            if (forceTargetBone != null || characterRoot == null)
            {
                return;
            }

            forceTargetBone =
                FindDeep(characterRoot, "Spine2") ??
                FindDeep(characterRoot, "Spine1") ??
                FindDeep(characterRoot, "Spine") ??
                FindDeep(characterRoot, "Hips") ??
                characterRoot;
        }

        private void MoveCharacterForceTargetTo(Vector3 targetPosition)
        {
            ResolveForceTargetBone();
            if (ShouldUseCharacterForceTarget())
            {
                characterRoot.position += targetPosition - forceTargetBone.position;
                SyncUpperEndpointToForceBone();
                return;
            }

            if (upperEndpoint != null)
            {
                upperEndpoint.position = targetPosition;
            }
        }

        private void ApplyFeedbackDisplacement(Vector3 displacement)
        {
            if (ShouldUseCharacterForceTarget())
            {
                var follow = 1f - Mathf.Exp(-Mathf.Max(0.01f, characterFeedbackFollow) * Time.deltaTime);
                characterRoot.position += displacement * follow;
                SyncUpperEndpointToForceBone();
                return;
            }

            upperEndpoint.position += displacement;
        }

        private void SyncUpperEndpointToForceBone()
        {
            ResolveForceTargetBone();
            if (ShouldUseCharacterForceTarget() && upperEndpoint != null)
            {
                upperEndpoint.position = forceTargetBone.position;
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

        private Vector3 CalculateUpperVelocity()
        {
            if (upperEndpoint == null)
            {
                return Vector3.zero;
            }

            var position = upperEndpoint.position;
            if (!_hasLastUpperPosition || Time.deltaTime <= 0f)
            {
                _lastUpperPosition = position;
                _hasLastUpperPosition = true;
                return Vector3.zero;
            }

            var velocity = (position - _lastUpperPosition) / Time.deltaTime;
            _lastUpperPosition = position;
            return velocity;
        }

        private void OnDrawGizmos()
        {
            if (!_feedback.IsActive || upperEndpoint == null)
            {
                return;
            }

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(upperEndpoint.position, upperEndpoint.position + _feedback.TensionDirection);
            Gizmos.DrawWireSphere(_feedback.AdjacentConstraintPoint, 0.12f);
        }
    }
}
