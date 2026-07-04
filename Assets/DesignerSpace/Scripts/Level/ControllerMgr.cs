using System.Collections.Generic;
using UnityEngine;

namespace DesignerSpace
{
    /// <summary>
    /// 指针拾取控制器（双小球）。
    ///
    /// 进入游戏后在场景中生成两个小球：A 球对应屏幕左半边，B 球对应屏幕右半边。
    /// 玩家在屏幕左半边按下（鼠标/手指）→ 从该点向世界发射射线，把 A 球移动到命中点；
    /// 在右半边按下 → 移动 B 球。支持多点触控，可用左右手同时分别操作 A、B 球。
    /// 小球被放到 Ignore Raycast 层，避免射线打到它自身。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Anchor/Controller Mgr")]
    public sealed class ControllerMgr : MonoBehaviour
    {
        [Header("相机")]
        [Tooltip("用于把屏幕点转换成射线的相机；留空时自动使用 Camera.main")]
        [SerializeField] private Camera targetCamera;

        [Header("小球")]
        [Tooltip("小球半径（米）")]
        [SerializeField] private float ballRadius = 0.15f;

        [Tooltip("A 球（左侧）初始世界位置")]
        [SerializeField] private Vector3 ballSpawnPositionA = new Vector3(-0.3f, 0f, 0f);

        [Tooltip("B 球（右侧）初始世界位置")]
        [SerializeField] private Vector3 ballSpawnPositionB = new Vector3(0.3f, 0f, 0f);

        [Tooltip("A 球（左侧）颜色")]
        [SerializeField] private Color ballColorA = new Color(1f, 0.35f, 0.2f);

        [Tooltip("B 球（右侧）颜色")]
        [SerializeField] private Color ballColorB = new Color(0.2f, 0.55f, 1f);

        [Header("屏幕左右分界")]
        [Range(0f, 1f)]
        [Tooltip("屏幕宽度的归一化分界：指针 x 小于该比例算左侧（A 球），否则算右侧（B 球）")]
        [SerializeField] private float screenSplit = 0.5f;

        [Header("射线")]
        [Tooltip("射线最大检测距离")]
        [SerializeField] private float maxRayDistance = 1000f;

        [Tooltip("射线检测的层遮罩（默认检测所有层）")]
        [SerializeField] private LayerMask raycastMask = ~0;

        [Tooltip("勾选后从射线遮罩中排除主角所在的图层，避免小球被主角自身碰撞体挡住/黏住")]
        [SerializeField] private bool ignoreActorLayer = true;

        [Tooltip("要被射线忽略的主角图层名")]
        [SerializeField] private string actorLayerName = "Actor";

        [Tooltip("忽略所有带刚体的碰撞体（如主角布娃娃的每根骨头都带刚体），只让小球吸附到静态场景（岩壁/锚点）")]
        [SerializeField] private bool ignoreDynamicBodies = true;

        [Tooltip("是否命中触发器（Trigger）碰撞体")]
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        // 内置 “Ignore Raycast” 层，Physics.Raycast 默认忽略，用来避免射线打到小球自己。
        private const int IgnoreRaycastLayer = 2;

        // 复用的射线命中缓冲，避免每帧分配。
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[32];

        // 复用的按住指针屏幕坐标列表，避免每帧分配。
        private readonly List<Vector2> _heldPointers = new List<Vector2>(8);

        private Transform _ballA;
        private Transform _ballB;

        /// <summary>A 球（左侧）Transform，其他系统可读取其位置作为左手跟随目标。</summary>
        public Transform BallA => _ballA;

        /// <summary>B 球（右侧）Transform，其他系统可读取其位置作为右手跟随目标。</summary>
        public Transform BallB => _ballB;

        /// <summary>本帧左侧（A 球）是否被有效驱动（有指针在左半屏且命中场景）。</summary>
        public bool IsAActive { get; private set; }

        /// <summary>本帧右侧（B 球）是否被有效驱动（有指针在右半屏且命中场景）。</summary>
        public bool IsBActive { get; private set; }

        private void Start()
        {
            _ballA = CreateBall("ControllerBallA", ballSpawnPositionA, ballColorA);
            _ballB = CreateBall("ControllerBallB", ballSpawnPositionB, ballColorB);
        }

        private void Update()
        {
            IsAActive = false;
            IsBActive = false;

            CollectHeldPointers(_heldPointers);
            if (_heldPointers.Count == 0)
            {
                return;
            }

            Camera camera = ResolveCamera();
            if (camera == null)
            {
                Debug.LogWarning("[ControllerMgr] 未找到相机，无法进行射线检测。", this);
                return;
            }

            float splitX = Screen.width * screenSplit;
            for (int i = 0; i < _heldPointers.Count; i++)
            {
                Vector2 screenPosition = _heldPointers[i];
                Ray ray = camera.ScreenPointToRay(screenPosition);
                if (!TryPickHit(ray, out RaycastHit hit))
                {
                    continue;
                }

                bool isLeftSide = screenPosition.x < splitX;
                if (isLeftSide)
                {
                    if (_ballA != null)
                    {
                        _ballA.position = hit.point;
                    }
                    IsAActive = true;
                }
                else
                {
                    if (_ballB != null)
                    {
                        _ballB.position = hit.point;
                    }
                    IsBActive = true;
                }
            }
        }

        /// <summary>
        /// 沿射线取最近的“有效”命中点：跳过主角图层，以及（可选）所有带刚体的碰撞体，
        /// 从而让小球穿过主角布娃娃，只吸附到静态场景（岩壁 / 锚点）。
        /// </summary>
        private bool TryPickHit(Ray ray, out RaycastHit best)
        {
            best = default;
            int count = Physics.RaycastNonAlloc(ray, _hitBuffer, maxRayDistance, ResolveRaycastMask(), triggerInteraction);
            float bestDistance = float.PositiveInfinity;
            bool found = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = _hitBuffer[i];
                if (ignoreDynamicBodies && candidate.collider.attachedRigidbody != null)
                {
                    continue;
                }

                if (candidate.distance < bestDistance)
                {
                    bestDistance = candidate.distance;
                    best = candidate;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>返回实际使用的射线遮罩：在配置遮罩基础上按需排除主角所在图层。</summary>
        private int ResolveRaycastMask()
        {
            int mask = raycastMask.value;
            if (ignoreActorLayer && !string.IsNullOrEmpty(actorLayerName))
            {
                int actorLayer = LayerMask.NameToLayer(actorLayerName);
                if (actorLayer >= 0)
                {
                    mask &= ~(1 << actorLayer);
                }
            }

            return mask;
        }

        /// <summary>收集本帧所有按住的指针屏幕坐标（多点触控则多个，鼠标则最多一个）。</summary>
        private void CollectHeldPointers(List<Vector2> results)
        {
            results.Clear();

            if (Input.touchSupported && Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled)
                    {
                        results.Add(touch.position);
                    }
                }

                return;
            }

            if (Input.GetMouseButton(0))
            {
                results.Add(Input.mousePosition);
            }
        }

        private Transform CreateBall(string ballName, Vector3 spawnPosition, Color color)
        {
            GameObject ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = ballName;
            ballObject.layer = IgnoreRaycastLayer;

            // 小球仅用于显示，移除其碰撞体，确保它永远不会成为射线命中的交点。
            var ballCollider = ballObject.GetComponent<Collider>();
            if (ballCollider != null)
            {
                Destroy(ballCollider);
            }

            Transform ballTransform = ballObject.transform;
            ballTransform.position = spawnPosition;
            // 默认球体直径为 1，缩放到 radius * 2 即得到目标半径。
            ballTransform.localScale = Vector3.one * (ballRadius * 2f);

            var renderer = ballObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }

            return ballTransform;
        }

        private Camera ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            return targetCamera;
        }

        private void OnValidate()
        {
            if (ballRadius < 0.001f)
            {
                ballRadius = 0.001f;
            }
        }
    }
}
