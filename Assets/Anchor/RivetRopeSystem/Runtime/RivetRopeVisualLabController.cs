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

        [Header("Lab Motion")]
        [SerializeField] private bool autoMoveUpper = true;
        [SerializeField] private float autoMoveAmplitude = 1.6f;
        [SerializeField] private float autoMoveSpeed = 0.65f;
        [SerializeField] private float nudgeStep = 0.35f;
        [SerializeField] private float dragPickRadius = 0.45f;

        [Header("Lab UI")]
        [SerializeField] private Rect panelRect = new Rect(16f, 16f, 430f, 560f);
        [SerializeField] private LabPathPreset preset = LabPathPreset.SingleRivet;

        private RivetRopeSettings _settings;
        private RivetRopeVisualSettings _visualSettings;
        private Transform _dragTarget;
        private Vector3 _upperBasePosition;

        private void Start()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            _settings = config != null ? config.Settings : RivetRopeSettings.CreateDefault();
            _visualSettings = config != null ? config.VisualSettings : RivetRopeVisualSettings.CreateDefault();
            if (upperEndpoint != null)
            {
                _upperBasePosition = upperEndpoint.position;
            }

            ApplyRuntimeSettings(true);
            ApplyPreset();
        }

        private void Update()
        {
            UpdateAutoMotion();
            UpdateEndpointDrag();
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
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("上移")) NudgeUpper(Vector3.up);
            if (GUILayout.Button("下移")) NudgeUpper(Vector3.down);
            if (GUILayout.Button("左移")) NudgeUpper(Vector3.left);
            if (GUILayout.Button("右移")) NudgeUpper(Vector3.right);
            GUILayout.EndHorizontal();
            GUILayout.Label("也可以用鼠标拖动上下端点球体");

            GUILayout.Space(6f);
            GUILayout.Label("Visual Mode");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("程序化线条")) SetMode(RivetRopeVisualMode.ProceduralLine);
            if (GUILayout.Button("Verlet 绳段")) SetMode(RivetRopeVisualMode.VerletSegments);
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);
            DrawSlider("绳长", ref _settings.TotalRopeLength, 6f, 32f, true);
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

            switch (preset)
            {
                case LabPathPreset.Direct:
                    upperEndpoint.position = new Vector3(2.4f, 4.6f, 0f);
                    break;
                case LabPathPreset.SingleRivet:
                    upperEndpoint.position = new Vector3(2.2f, 4.8f, 0f);
                    SetMarkerPosition(0, new Vector3(0f, 0.9f, 0f));
                    break;
                case LabPathPreset.MultiRivet:
                    upperEndpoint.position = new Vector3(2.5f, 5.2f, 0f);
                    SetMarkerPosition(0, new Vector3(-1.2f, -0.6f, 0f));
                    SetMarkerPosition(1, new Vector3(0.9f, 1.5f, 0f));
                    SetMarkerPosition(2, new Vector3(-0.2f, 3.2f, 0f));
                    break;
                case LabPathPreset.SharpTurn:
                    upperEndpoint.position = new Vector3(2.8f, 4.8f, 0f);
                    SetMarkerPosition(0, new Vector3(-1.8f, 0.4f, 0f));
                    SetMarkerPosition(1, new Vector3(1.8f, 1.2f, 0f));
                    SetMarkerPosition(2, new Vector3(-1.2f, 2.8f, 0f));
                    break;
            }

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
            upperEndpoint.position += direction * nudgeStep;
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
            upperEndpoint.position = _upperBasePosition + offset;
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
                _dragTarget.position = MouseWorldPosition(_dragTarget.position.z);
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
    }
}
