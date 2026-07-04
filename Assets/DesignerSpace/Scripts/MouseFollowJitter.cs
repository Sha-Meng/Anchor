using UnityEngine;

namespace DesignerSpace
{
    /// <summary>拖注物当前的抖动档位</summary>
    public enum JitterTier
    {
        /// <summary>档位 1：剧烈抖动（距离最近锚点 &lt;= intenseRadius）</summary>
        Intense = 1,
        /// <summary>档位 2：轻微抖动（intenseRadius &lt; 距离 &lt;= slightRadius）</summary>
        Slight = 2,
        /// <summary>档位 3：完全不抖动（距离 &gt; slightRadius）</summary>
        None = 3
    }

    /// <summary>
    /// 预研：鼠标跟随附着物。
    /// 每帧将鼠标屏幕坐标投影到 XZ 地面平面上，
    /// 然后依据到场景中所有 AnchorPoint 的最短距离判定抖动档位（剧烈/轻微/不抖动），
    /// 并叠加对应强度的位置抖动。
    /// </summary>
    [DisallowMultipleComponent]
    public class MouseFollowJitter : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("用于鼠标投影的摄像机，留空则使用 Camera.main")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("鼠标投影所在的水平面高度（Y 值），需与地面 Plane 保持一致")]
        [SerializeField] private float planeHeight = 0f;

        [Tooltip("附着物悬浮在地面之上的高度")]
        [SerializeField] private float hoverHeight = 0.3f;

        [Header("抖动分档阈值（到最近锚点的距离）")]
        [Tooltip("小于等于该距离：档位 1 剧烈抖动")]
        [SerializeField] private float intenseRadius = 1.0f;

        [Tooltip("小于等于该距离（且大于剧烈半径）：档位 2 轻微抖动；超出则档位 3 完全不抖动")]
        [SerializeField] private float slightRadius = 2.5f;

        [Header("抖动强度")]
        [SerializeField] private float intenseJitterAmplitude = 0.35f;
        [SerializeField] private float slightJitterAmplitude = 0.08f;
        [Tooltip("抖动噪声采样频率，数值越大抖动越\u201c碎\u201d，越小越\u201c荡\u201d")]
        [SerializeField] private float jitterFrequency = 25f;

        [Header("跟随手感")]
        [Tooltip("跟随平滑速度，数值越大跟随越紧；<= 0 表示瞬间跟随")]
        [SerializeField] private float followLerpSpeed = 20f;

        [Header("三档视觉反馈（便于预研阶段肉眼验证，正式版可关闭）")]
        [SerializeField] private bool colorFeedback = true;
        [SerializeField] private Color intenseColor = new Color(1f, 0.15f, 0.15f);
        [SerializeField] private Color slightColor = new Color(1f, 0.85f, 0.1f);
        [SerializeField] private Color noneColor = new Color(0.2f, 1f, 0.4f);

        private AnchorPoint[] _anchors;
        private Renderer _renderer;
        private Vector3 _smoothedTarget;
        private float _noiseSeedX;
        private float _noiseSeedZ;
        private bool _hasTarget;

        /// <summary>当前抖动档位，可供其他系统（如音效/手柄振动）读取</summary>
        public JitterTier CurrentTier { get; private set; } = JitterTier.None;

        /// <summary>当前到最近锚点的距离</summary>
        public float DistanceToNearestAnchor { get; private set; } = Mathf.Infinity;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _noiseSeedX = Random.Range(0f, 1000f);
            _noiseSeedZ = Random.Range(0f, 1000f);
        }

        private void OnEnable()
        {
            RefreshAnchorCache();
        }

        private void Update()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
            if (targetCamera == null)
            {
                return;
            }

            var groundPlane = new Plane(Vector3.up, new Vector3(0f, planeHeight, 0f));
            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);

            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                if (!_hasTarget || followLerpSpeed <= 0f)
                {
                    _smoothedTarget = hitPoint;
                    _hasTarget = true;
                }
                else
                {
                    float t = 1f - Mathf.Exp(-followLerpSpeed * Time.deltaTime);
                    _smoothedTarget = Vector3.Lerp(_smoothedTarget, hitPoint, t);
                }
            }

            if (!_hasTarget)
            {
                return;
            }

            DistanceToNearestAnchor = FindNearestAnchorDistance(_smoothedTarget);
            CurrentTier = EvaluateTier(DistanceToNearestAnchor);

            Vector3 jitterOffset = ComputeJitterOffset(CurrentTier);
            Vector3 finalPosition = _smoothedTarget + jitterOffset;
            finalPosition.y = planeHeight + hoverHeight;
            transform.position = finalPosition;

            ApplyColorFeedback();
        }

        private void RefreshAnchorCache()
        {
            _anchors = FindObjectsOfType<AnchorPoint>();
        }

        private float FindNearestAnchorDistance(Vector3 worldPosition)
        {
            if (_anchors == null || _anchors.Length == 0)
            {
                RefreshAnchorCache();
                if (_anchors == null || _anchors.Length == 0)
                {
                    return Mathf.Infinity;
                }
            }

            float nearestSqr = Mathf.Infinity;
            Vector2 flatPosition = new Vector2(worldPosition.x, worldPosition.z);
            for (int i = 0; i < _anchors.Length; i++)
            {
                if (_anchors[i] == null)
                {
                    continue;
                }
                Vector3 anchorPosition = _anchors[i].transform.position;
                Vector2 flatAnchor = new Vector2(anchorPosition.x, anchorPosition.z);
                float sqrDistance = (flatPosition - flatAnchor).sqrMagnitude;
                if (sqrDistance < nearestSqr)
                {
                    nearestSqr = sqrDistance;
                }
            }

            return Mathf.Sqrt(nearestSqr);
        }

        private JitterTier EvaluateTier(float distance)
        {
            if (distance <= intenseRadius)
            {
                return JitterTier.Intense;
            }
            if (distance <= slightRadius)
            {
                return JitterTier.Slight;
            }
            return JitterTier.None;
        }

        private Vector3 ComputeJitterOffset(JitterTier tier)
        {
            float amplitude;
            switch (tier)
            {
                case JitterTier.Intense:
                    amplitude = intenseJitterAmplitude;
                    break;
                case JitterTier.Slight:
                    amplitude = slightJitterAmplitude;
                    break;
                default:
                    amplitude = 0f;
                    break;
            }

            if (amplitude <= 0f)
            {
                return Vector3.zero;
            }

            float t = Time.time * jitterFrequency;
            float noiseX = Mathf.PerlinNoise(_noiseSeedX, t) * 2f - 1f;
            float noiseZ = Mathf.PerlinNoise(_noiseSeedZ, t) * 2f - 1f;
            return new Vector3(noiseX, 0f, noiseZ) * amplitude;
        }

        private void ApplyColorFeedback()
        {
            if (!colorFeedback || _renderer == null)
            {
                return;
            }

            Color targetColor;
            switch (CurrentTier)
            {
                case JitterTier.Intense:
                    targetColor = intenseColor;
                    break;
                case JitterTier.Slight:
                    targetColor = slightColor;
                    break;
                default:
                    targetColor = noneColor;
                    break;
            }

            if (_renderer.material.color != targetColor)
            {
                _renderer.material.color = targetColor;
            }
        }
    }
}
