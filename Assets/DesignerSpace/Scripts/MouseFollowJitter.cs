using UnityEngine;

namespace DesignerSpace
{
    /// <summary>附着物当前的抖动档位</summary>
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
    /// 预研：鼠标探测 + 墙面吸附附着物。
    ///
    /// 交互流程：
    /// 1. 未按下左键时，小球跟随鼠标在墙面上的投影点滑动（贴着墙面，不抖动）。
    /// 2. 按住鼠标左键时，探测当前墙面点到场景中所有 AnchorPoint 的最短距离，
    ///    按距离分三档（剧烈 / 轻微 / 不抖动）在墙面内叠加抖动，同时小球吸附锁定在墙面上。
    /// 3. 松开左键，小球恢复跟随滑动、停止抖动。
    ///
    /// 墙面点通过对 wallMask 层的物理射线检测得到，命中面的法线即吸附朝向，
    /// 不依赖任何硬编码的平面朝向，墙面可任意摆放/旋转。
    /// </summary>
    [DisallowMultipleComponent]
    public class MouseFollowJitter : MonoBehaviour
    {
        [Header("参照")]
        [Tooltip("用于鼠标投影的摄像机，留空则使用 Camera.main")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("墙面所在的物理层。鼠标射线只与该层碰撞体求交，得到墙面上的投影点与法线。")]
        [SerializeField] private LayerMask wallMask = ~0;

        [Tooltip("鼠标射线的最大检测距离")]
        [SerializeField] private float maxRayDistance = 200f;

        [Tooltip("小球沿墙面法线方向悬浮的高度（避免与墙面 Z-fighting）")]
        [SerializeField] private float hoverHeight = 0.3f;

        [Header("探测触发")]
        [Tooltip("勾选后需要按住鼠标左键才探测+抖动+吸附；取消则始终探测（用于快速调试）")]
        [SerializeField] private bool requireMouseButton = true;

        [Tooltip("用于触发探测的鼠标按键（0=左键，1=右键，2=中键）")]
        [SerializeField] private int probeMouseButton = 0;

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
        [SerializeField] private Color idleColor = new Color(0.75f, 0.8f, 0.85f);
        [SerializeField] private Color intenseColor = new Color(1f, 0.15f, 0.15f);
        [SerializeField] private Color slightColor = new Color(1f, 0.85f, 0.1f);
        [SerializeField] private Color noneColor = new Color(0.2f, 1f, 0.4f);

        [Header("布娃娃手部同步")]
        [Tooltip("需要同步到 MouseCursorFollower 位置的手部刚体。优先使用刚体，避免只改 Transform 导致物理状态残留。")]
        [SerializeField] private Rigidbody targetHandRigidbody;

        [Tooltip("没有刚体时使用的备用 Transform。")]
        [SerializeField] private Transform targetHandTransform;

        [Tooltip("勾选后只有探测按键按住时才同步手部；默认关闭表示光标球移动时手始终跟随。")]
        [SerializeField] private bool syncHandOnlyWhileProbing = false;

        [Tooltip("同步位置时清零手部刚体速度，避免刚体被上一帧速度甩离光标。")]
        [SerializeField] private bool clearHandVelocityOnSync = true;

        private AnchorPoint[] _anchors;
        private Renderer _renderer;
        private Vector3 _smoothedTarget;
        private Vector3 _lastWallNormal = Vector3.back;
        private float _noiseSeedX;
        private float _noiseSeedZ;
        private bool _hasTarget;
        private bool _isProbing;

        /// <summary>当前抖动档位，可供其他系统（如音效/手柄振动）读取</summary>
        public JitterTier CurrentTier { get; private set; } = JitterTier.None;

        /// <summary>当前到最近锚点的距离</summary>
        public float DistanceToNearestAnchor { get; private set; } = Mathf.Infinity;

        /// <summary>当前是否正处于探测（吸附）状态</summary>
        public bool IsProbing => _isProbing;

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

            _isProbing = !requireMouseButton || Input.GetMouseButton(probeMouseButton);

            UpdateWallTarget();

            if (!_hasTarget)
            {
                return;
            }

            if (_isProbing)
            {
                // 探测态：算距离、分档、抖动、吸附锁定在墙面上。
                DistanceToNearestAnchor = FindNearestAnchorDistance(_smoothedTarget);
                CurrentTier = EvaluateTier(DistanceToNearestAnchor);
                Vector3 jitterOffset = ComputeJitterOffset(CurrentTier);
                transform.position = _smoothedTarget + jitterOffset + _lastWallNormal * hoverHeight;
            }
            else
            {
                // 空闲态：只跟随滑动，不抖动。
                CurrentTier = JitterTier.None;
                DistanceToNearestAnchor = Mathf.Infinity;
                transform.position = _smoothedTarget + _lastWallNormal * hoverHeight;
            }

            ApplyColorFeedback();
        }

        private void LateUpdate()
        {
            SyncHandPosition();
        }

        /// <summary>
        /// 用鼠标射线打墙面碰撞体，更新墙面命中点 _smoothedTarget 与命中法线 _lastWallNormal。
        /// </summary>
        private void UpdateWallTarget()
        {
            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, wallMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            _lastWallNormal = hit.normal;

            if (!_hasTarget || followLerpSpeed <= 0f)
            {
                _smoothedTarget = hit.point;
                _hasTarget = true;
            }
            else
            {
                float t = 1f - Mathf.Exp(-followLerpSpeed * Time.deltaTime);
                _smoothedTarget = Vector3.Lerp(_smoothedTarget, hit.point, t);
            }
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

            // 直接用世界三维距离比较即可：锚点与探测点都贴在墙面上，
            // 沿法线方向的差异极小，不再依赖任何投影面轴向假设。
            float nearestSqr = Mathf.Infinity;
            for (int i = 0; i < _anchors.Length; i++)
            {
                if (_anchors[i] == null)
                {
                    continue;
                }
                float sqrDistance = (worldPosition - _anchors[i].transform.position).sqrMagnitude;
                if (sqrDistance < nearestSqr)
                {
                    nearestSqr = sqrDistance;
                }
            }

            return Mathf.Sqrt(nearestSqr);
        }

        private void SyncHandPosition()
        {
            if (!_hasTarget || (syncHandOnlyWhileProbing && !_isProbing))
            {
                return;
            }

            Vector3 targetPosition = transform.position;
            if (targetHandRigidbody != null)
            {
                targetHandRigidbody.position = targetPosition;
                if (clearHandVelocityOnSync)
                {
                    targetHandRigidbody.velocity = Vector3.zero;
                    targetHandRigidbody.angularVelocity = Vector3.zero;
                }
                return;
            }

            if (targetHandTransform != null)
            {
                targetHandTransform.position = targetPosition;
            }
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

            // 在墙面切平面内抖动：以命中法线构造两个正交切向基，
            // 保证抖动始终贴着墙面晃，而不是往墙里/墙外穿。
            Vector3 tangent = Vector3.Cross(_lastWallNormal, Vector3.up);
            if (tangent.sqrMagnitude < 1e-4f)
            {
                tangent = Vector3.Cross(_lastWallNormal, Vector3.right);
            }
            tangent.Normalize();
            Vector3 bitangent = Vector3.Cross(_lastWallNormal, tangent);

            float t = Time.time * jitterFrequency;
            float noiseU = Mathf.PerlinNoise(_noiseSeedX, t) * 2f - 1f;
            float noiseV = Mathf.PerlinNoise(_noiseSeedZ, t) * 2f - 1f;
            return (tangent * noiseU + bitangent * noiseV) * amplitude;
        }

        private void ApplyColorFeedback()
        {
            if (!colorFeedback || _renderer == null)
            {
                return;
            }

            Color targetColor;
            if (!_isProbing)
            {
                targetColor = idleColor;
            }
            else
            {
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
            }

            if (_renderer.material.color != targetColor)
            {
                _renderer.material.color = targetColor;
            }
        }
    }
}
