using UnityEngine;

namespace DesignerSpace
{
    /// <summary>
    /// 运行时把某个 <see cref="AnchorPoint"/> 的核心半径与最大半径以圆环形式画到 Game 视图。
    ///
    /// Gizmos 只在 Scene 视图/编辑器可见，无法满足"在 Game 显示"的需求；这里改用 <see cref="LineRenderer"/>
    /// 在锚点所在墙面的切平面内生成两个圆环（核心/最大）以及一个中心点，运行态与打包后都可见。
    /// 通常由 <see cref="RouteNetwork"/> 在 Debug 模式下按需挂载并调用 <see cref="Configure"/>。
    /// Release 引导点可调用 <see cref="ConfigureCenterOnly"/> 只显示中心点。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AnchorPoint))]
    public class AnchorRadiusVisualizer : MonoBehaviour
    {
        private const string CoreRingName = "__CoreRadiusRing";
        private const string MaxRingName = "__MaxRadiusRing";
        private const string CenterName = "__CenterDot";

        private AnchorPoint _anchor;
        private Color _coreColor = new Color(1f, 0.15f, 0.15f, 0.9f);
        private Color _maxColor = new Color(1f, 0.85f, 0.1f, 0.75f);
        private Color _centerColor = Color.white;
        private int _segments = 48;
        private float _lineWidth = 0.03f;
        private bool _showRadiusRings = true;

        private Material _ringMaterial;

        /// <summary>
        /// 使用配置刷新可视化参数并重建圆环。
        /// </summary>
        public void Configure(Color coreColor, Color maxColor, Color centerColor, int segments, float lineWidth)
        {
            _coreColor = coreColor;
            _maxColor = maxColor;
            _centerColor = centerColor;
            _segments = Mathf.Max(8, segments);
            _lineWidth = Mathf.Max(0.001f, lineWidth);
            _showRadiusRings = true;
            SetLineActive(CoreRingName, true);
            SetLineActive(MaxRingName, true);
            Rebuild();
        }

        /// <summary>
        /// 只显示锚点中心，用于 Release 模式下给玩家做路线引导，不暴露半径信息。
        /// </summary>
        public void ConfigureCenterOnly(Color centerColor, int segments, float lineWidth)
        {
            _centerColor = centerColor;
            _segments = Mathf.Max(8, segments);
            _lineWidth = Mathf.Max(0.001f, lineWidth);
            _showRadiusRings = false;
            SetLineActive(CoreRingName, false);
            SetLineActive(MaxRingName, false);
            Rebuild();
        }

        private void Awake()
        {
            _anchor = GetComponent<AnchorPoint>();
        }

        private void OnEnable()
        {
            Rebuild();
        }

        /// <summary>根据锚点当前半径重新生成所有圆环与中心点。</summary>
        public void Rebuild()
        {
            if (_anchor == null)
            {
                _anchor = GetComponent<AnchorPoint>();
            }
            if (_anchor == null)
            {
                return;
            }

            if (_showRadiusRings)
            {
                LineRenderer core = EnsureLine(CoreRingName, _coreColor);
                LineRenderer max = EnsureLine(MaxRingName, _maxColor);

                BuildCircle(core, _anchor.previewIntenseRadius);
                BuildCircle(max, _anchor.previewSlightRadius);
            }
            else
            {
                SetLineActive(CoreRingName, false);
                SetLineActive(MaxRingName, false);
            }

            LineRenderer center = EnsureLine(CenterName, _centerColor);

            BuildCircle(center, Mathf.Max(0.05f, _lineWidth * 2f));
        }

        private void SetLineActive(string childName, bool active)
        {
            Transform existing = transform.Find(childName);
            if (existing != null && existing.gameObject.activeSelf != active)
            {
                existing.gameObject.SetActive(active);
            }
        }

        private LineRenderer EnsureLine(string childName, Color color)
        {
            Transform existing = transform.Find(childName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(childName);
                go.transform.SetParent(transform, false);
            }

            var line = go.GetComponent<LineRenderer>();
            if (line == null)
            {
                line = go.AddComponent<LineRenderer>();
            }

            if (_ringMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    shader = Shader.Find("Unlit/Color");
                }
                _ringMaterial = new Material(shader);
            }

            line.useWorldSpace = true;
            line.loop = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCornerVertices = 0;
            line.numCapVertices = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = _ringMaterial;
            line.widthMultiplier = _lineWidth;
            line.startColor = color;
            line.endColor = color;
            return line;
        }

        private void BuildCircle(LineRenderer line, float radius)
        {
            if (line == null)
            {
                return;
            }

            radius = Mathf.Max(0f, radius);

            Vector3 normal = transform.forward;
            if (normal.sqrMagnitude < 1e-4f)
            {
                normal = Vector3.forward;
            }
            normal.Normalize();

            // 圆环画在墙面切平面内：以锚点法线构造两个正交切向基。
            Vector3 tangent = Vector3.Cross(normal, Vector3.up);
            if (tangent.sqrMagnitude < 1e-4f)
            {
                tangent = Vector3.Cross(normal, Vector3.right);
            }
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;

            Vector3 center = transform.position;
            line.positionCount = _segments;
            for (int i = 0; i < _segments; i++)
            {
                float angle = (float)i / _segments * Mathf.PI * 2f;
                Vector3 offset = (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * radius;
                line.SetPosition(i, center + offset);
            }
        }

        private void OnDestroy()
        {
            if (_ringMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_ringMaterial);
                }
                else
                {
                    DestroyImmediate(_ringMaterial);
                }
            }
        }
    }
}
