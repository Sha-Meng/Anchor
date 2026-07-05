using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DesignerSpace
{
    public enum ControllerBallState
    {
        Anchored,
        Releasing,
        Hooked
    }

    /// <summary>
    /// 指针拾取控制器（双小球）。
    ///
    /// 进入游戏后在场景中生成两个小球。玩家按下（鼠标/手指）时，从该点向世界发射射线，
    /// 射线命中点离哪个小球更近，就把哪个小球移动到命中点。
    /// 支持多点触控，可同时分别操作 A、B 球。
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

        [Tooltip("A 球初始位置参考点；拖拽场景中的 Transform 后，运行时优先使用它的世界坐标")]
        [SerializeField] private Transform ballSpawnPointA;

        [Tooltip("B 球初始位置参考点；拖拽场景中的 Transform 后，运行时优先使用它的世界坐标")]
        [SerializeField] private Transform ballSpawnPointB;

        [Tooltip("A 球初始世界位置（未配置 Transform 时使用）")]
        [SerializeField] private Vector3 ballSpawnPositionA = new Vector3(-0.3f, 0f, 0f);

        [Tooltip("B 球初始世界位置（未配置 Transform 时使用）")]
        [SerializeField] private Vector3 ballSpawnPositionB = new Vector3(0.3f, 0f, 0f);

        [Tooltip("A 球颜色")]
        [SerializeField] private Color ballColorA = new Color(1f, 0.35f, 0.2f);

        [Tooltip("B 球颜色")]
        [SerializeField] private Color ballColorB = new Color(0.2f, 0.55f, 1f);

        [Header("小球释放")]
        [Tooltip("释放中小球移动到待机点的速度；<= 0 表示瞬间到位")]
        [SerializeField] private float releaseMoveSpeed = 8f;

        [Tooltip("释放待机点相对两个小球屏幕中点向下偏移的像素")]
        [SerializeField] private float releaseStandbyScreenDownOffset = 160f;

        [Header("双球距离约束")]
        [Tooltip("开启后：hook（拖动）某个小球时，被拖动的球会被限制在“以另一球为球心、最大距离为半径”的范围内，避免两球距离过远")]
        [SerializeField] private bool constrainBallDistance = true;

        [Tooltip("两球之间允许的最大距离（米）；仅夹紧本帧被拖动的那个球，另一球保持不动")]
        [Min(0f)]
        [SerializeField] private float maxBallDistance = 2f;

        [Header("运行时状态显示")]
        [Tooltip("A 球当前状态，仅用于运行时观察")]
        [SerializeField] private ControllerBallState ballAStateView = ControllerBallState.Anchored;

        [Tooltip("B 球当前状态，仅用于运行时观察")]
        [SerializeField] private ControllerBallState ballBStateView = ControllerBallState.Anchored;

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

        [Header("UI 输入互斥")]
        [Tooltip("开启后，指针从 UI 上按下时不会触发场景射线点击，直到该指针松开")]
        [SerializeField] private bool ignoreUiPointerInput = true;

        [Header("音效")]
        [Tooltip("任意小球处于 Hook 中时循环播放的摸索音效")]
        [SerializeField] private AudioClip hookLoopClip;

        [SerializeField, Range(0f, 1f)] private float hookLoopVolume = 1f;

        [Header("手机震动")]
        [Tooltip("Hook 小球靠近锚点时使用的手机震动适配器；留空时优先查找同物体上的适配器，其次查找场景中的适配器")]
        [SerializeField] private MonoBehaviour hookHaptics;

        [Tooltip("震动强度曲线指数；大于 1 时只有更接近锚点才明显变强")]
        [Min(0.1f)]
        [SerializeField] private float hookHapticIntensityExponent = 1f;

        // 内置 “Ignore Raycast” 层，Physics.Raycast 默认忽略，用来避免射线打到小球自己。
        private const int IgnoreRaycastLayer = 2;
        private const string HapticAdapterTypeName = "MobileHapticFeedbackAdapter";

        // 复用的射线命中缓冲，避免每帧分配。
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[32];

        private readonly List<PointerSample> _pointerSamples = new List<PointerSample>(8);
        private readonly HashSet<int> _uiCapturedPointerIds = new HashSet<int>();
        private readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>();

        private Transform _ballA;
        private Transform _ballB;
        private BallRuntime _ballARuntime;
        private BallRuntime _ballBRuntime;
        private AudioSource _hookLoopSource;
        private AnchorPoint[] _anchorPoints = Array.Empty<AnchorPoint>();
        private bool _hookHapticsActive;
        private bool _mouseStartedOverUi;
        private PointerEventData _uiPointerData;

        /// <summary>A 球 Transform，其他系统可读取其位置作为左手跟随目标。</summary>
        public Transform BallA => _ballA;

        /// <summary>B 球 Transform，其他系统可读取其位置作为右手跟随目标。</summary>
        public Transform BallB => _ballB;

        /// <summary>本帧 A 球是否被有效驱动（有指针命中场景且命中点更接近 A 球）。</summary>
        public bool IsAActive { get; private set; }

        /// <summary>本帧 B 球是否被有效驱动（有指针命中场景且命中点更接近 B 球）。</summary>
        public bool IsBActive { get; private set; }

        public ControllerBallState BallAState => _ballARuntime.State;

        public ControllerBallState BallBState => _ballBRuntime.State;

        /// <summary>小球进入 Releasing 状态时触发，参数为要扣除的最大体力比例。</summary>
        public event Action<float> ReleaseStaminaPenaltyRequested;

        private void Start()
        {
            _ballA = CreateBall("ControllerBallA", ResolveSpawnPosition(ballSpawnPointA, ballSpawnPositionA), ballColorA);
            _ballB = CreateBall("ControllerBallB", ResolveSpawnPosition(ballSpawnPointB, ballSpawnPositionB), ballColorB);
            _ballARuntime = new BallRuntime(ControllerBallState.Anchored);
            _ballBRuntime = new BallRuntime(ControllerBallState.Anchored);
            RefreshAnchorPointCache();
            RefreshBallStateView();
        }

        private void Update()
        {
            Camera camera = ResolveCamera();
            if (camera == null)
            {
                Debug.LogWarning("[ControllerMgr] 未找到相机，无法进行射线检测。", this);
                return;
            }

            IsAActive = false;
            IsBActive = false;

            CollectPointerSamples(_pointerSamples);
            for (int i = 0; i < _pointerSamples.Count; i++)
            {
                HandlePointerSample(_pointerSamples[i], camera);
            }

            UpdateReleasingBall(_ballA, ref _ballARuntime);
            UpdateReleasingBall(_ballB, ref _ballBRuntime);
            RefreshBallStateView();
            UpdateHookLoopAudio();
            UpdateHookHapticFeedback();
        }

        private void OnDisable()
        {
            StopHookLoopAudio();
            ClearHookHapticFeedback();
        }

        private void RefreshBallStateView()
        {
            ballAStateView = _ballARuntime.State;
            ballBStateView = _ballBRuntime.State;
        }

        private void HandlePointerSample(PointerSample sample, Camera camera)
        {
            if (sample.Phase == PointerPhase.Began)
            {
                TryBeginHook(sample, camera);
                return;
            }

            if (sample.Phase == PointerPhase.Moved)
            {
                TryMoveHook(sample, camera);
                return;
            }

            ReleasePointer(sample.PointerId);
        }

        private void TryBeginHook(PointerSample sample, Camera camera)
        {
            Ray ray = camera.ScreenPointToRay(sample.ScreenPosition);
            if (!TryPickHit(ray, out RaycastHit hit))
            {
                return;
            }

            if (IsHitCloserToBallA(hit.point))
            {
                TryAssignPointerToBall(sample.PointerId, ref _ballARuntime, _ballBRuntime);
                TryMoveBallToHit(_ballA, ref _ballARuntime, _ballB, hit.point, true);
                return;
            }

            TryAssignPointerToBall(sample.PointerId, ref _ballBRuntime, _ballARuntime);
            TryMoveBallToHit(_ballB, ref _ballBRuntime, _ballA, hit.point, false);
        }

        private void TryMoveHook(PointerSample sample, Camera camera)
        {
            Ray ray = camera.ScreenPointToRay(sample.ScreenPosition);
            if (!TryPickHit(ray, out RaycastHit hit))
            {
                return;
            }

            if (_ballARuntime.IsControlledBy(sample.PointerId))
            {
                TryMoveBallToHit(_ballA, ref _ballARuntime, _ballB, hit.point, true);
                return;
            }

            if (_ballBRuntime.IsControlledBy(sample.PointerId))
            {
                TryMoveBallToHit(_ballB, ref _ballBRuntime, _ballA, hit.point, false);
            }
        }

        private void TryAssignPointerToBall(int pointerId, ref BallRuntime ball, BallRuntime other)
        {
            if (other.IsControlledBy(pointerId) ||
                (ball.State == ControllerBallState.Hooked && !ball.IsControlledBy(pointerId)))
            {
                return;
            }

            ball.HasReleaseTarget = false;
            ball.PointerId = pointerId;
            ball.State = ControllerBallState.Hooked;
        }

        private void TryMoveBallToHit(
            Transform ball,
            ref BallRuntime runtime,
            Transform other,
            Vector3 hitPoint,
            bool isBallA)
        {
            if (ball == null || runtime.State != ControllerBallState.Hooked)
            {
                return;
            }

            ball.position = ClampToOther(hitPoint, other);
            if (isBallA)
            {
                IsAActive = true;
            }
            else
            {
                IsBActive = true;
            }
        }

        private void UpdateReleasingBall(Transform ball, ref BallRuntime runtime)
        {
            if (ball == null || runtime.State != ControllerBallState.Releasing || !runtime.HasReleaseTarget)
            {
                return;
            }

            if (releaseMoveSpeed <= 0f)
            {
                ball.position = runtime.ReleaseTarget;
                runtime.HasReleaseTarget = false;
                return;
            }

            ball.position = Vector3.MoveTowards(ball.position, runtime.ReleaseTarget, releaseMoveSpeed * Time.deltaTime);
            if ((ball.position - runtime.ReleaseTarget).sqrMagnitude <= 0.000001f)
            {
                ball.position = runtime.ReleaseTarget;
                runtime.HasReleaseTarget = false;
            }
        }

        private void ReleasePointer(int pointerId)
        {
            if (_ballARuntime.IsControlledBy(pointerId))
            {
                FinishHookAndSettle(_ballA, ref _ballARuntime, _ballB);
            }

            if (_ballBRuntime.IsControlledBy(pointerId))
            {
                FinishHookAndSettle(_ballB, ref _ballBRuntime, _ballA);
            }
        }

        private void FinishHookAndSettle(
            Transform ball,
            ref BallRuntime runtime,
            Transform other)
        {
            if (runtime.State != ControllerBallState.Hooked)
            {
                return;
            }

            runtime.ClearPointer();
            if (ball == null)
            {
                EnterReleasingState(ref runtime);
                return;
            }

            if (TryFindAnchorPoint(ball.position, out AnchorPoint anchorPoint))
            {
                ball.position = anchorPoint.transform.position;
                runtime.State = ControllerBallState.Anchored;
                runtime.HasReleaseTarget = false;

                LevelSettlement.RequestSettlement(anchorPoint);
                return;
            }

            EnterReleasing(ball, ref runtime, other);
        }

        private void EnterReleasing(Transform ball, ref BallRuntime runtime, Transform other)
        {
            EnterReleasingState(ref runtime);
            runtime.ClearPointer();
            runtime.HasReleaseTarget = TryCalculateReleaseTarget(ball, other, out runtime.ReleaseTarget);
        }

        private void EnterReleasingState(ref BallRuntime runtime)
        {
            if (runtime.State == ControllerBallState.Releasing)
            {
                return;
            }

            runtime.State = ControllerBallState.Releasing;
            ReleaseStaminaPenaltyRequested?.Invoke(ResolveReleasingStaminaPenaltyRatio());
        }

        private float ResolveReleasingStaminaPenaltyRatio()
        {
            LevelMgr levelMgr = LevelMgr.Instance;
            LevelGlobalConfig config = levelMgr != null ? levelMgr.Config : null;
            return config != null ? config.ReleasingStaminaPenaltyRatio : 0.4f;
        }

        private bool TryCalculateReleaseTarget(Transform ball, Transform other, out Vector3 target)
        {
            target = ball != null ? ball.position : default;
            Camera camera = ResolveCamera();
            if (camera == null || ball == null || other == null)
            {
                return false;
            }

            Vector3 ballScreen = camera.WorldToScreenPoint(ball.position);
            Vector3 otherScreen = camera.WorldToScreenPoint(other.position);
            Vector3 standbyScreen = (ballScreen + otherScreen) * 0.5f;
            standbyScreen.y -= releaseStandbyScreenDownOffset;

            Ray ray = camera.ScreenPointToRay(standbyScreen);
            if (!TryPickHit(ray, out RaycastHit hit))
            {
                return false;
            }

            target = hit.point;
            return true;
        }

        private bool TryFindAnchorPoint(Vector3 position, out AnchorPoint bestAnchor)
        {
            bestAnchor = null;
            float bestDistanceSqr = Mathf.Infinity;
            AnchorPoint[] anchors = FindObjectsOfType<AnchorPoint>();
            for (int i = 0; i < anchors.Length; i++)
            {
                AnchorPoint anchor = anchors[i];
                if (anchor == null || !anchor.isActiveAndEnabled)
                {
                    continue;
                }

                float outerRadius = Mathf.Max(0f, anchor.previewSlightRadius);
                if (outerRadius <= 0f)
                {
                    continue;
                }

                float distanceSqr = (position - anchor.transform.position).sqrMagnitude;
                if (distanceSqr > outerRadius * outerRadius || distanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestAnchor = anchor;
                bestDistanceSqr = distanceSqr;
            }

            return bestAnchor != null;
        }

        private void UpdateHookHapticFeedback()
        {
            float strength = 0f;
            if (_ballARuntime.State == ControllerBallState.Hooked)
            {
                strength = Mathf.Max(strength, ResolveHookHapticStrength(_ballA));
            }

            if (_ballBRuntime.State == ControllerBallState.Hooked)
            {
                strength = Mathf.Max(strength, ResolveHookHapticStrength(_ballB));
            }

            if (strength <= 0f)
            {
                ClearHookHapticFeedback();
                return;
            }

            MonoBehaviour adapter = ResolveHookHaptics();
            if (adapter == null)
            {
                return;
            }

            SetHookHapticStrength(adapter, strength);
            _hookHapticsActive = true;
        }

        private float ResolveHookHapticStrength(Transform ball)
        {
            if (ball == null || !TryFindNearestAnchorPoint(ball.position, out AnchorPoint anchor, out float distance))
            {
                return 0f;
            }

            float outerRadius = Mathf.Max(0f, anchor.previewSlightRadius);
            if (outerRadius <= 0f || distance >= outerRadius)
            {
                return 0f;
            }

            float fullStrengthRadius = Mathf.Clamp(anchor.previewIntenseRadius, 0f, outerRadius);
            if (fullStrengthRadius >= outerRadius)
            {
                return 1f;
            }

            float t = Mathf.InverseLerp(outerRadius, fullStrengthRadius, distance);
            return Mathf.Pow(Mathf.Clamp01(t), hookHapticIntensityExponent);
        }

        private bool TryFindNearestAnchorPoint(Vector3 position, out AnchorPoint nearestAnchor, out float nearestDistance)
        {
            nearestAnchor = null;
            nearestDistance = Mathf.Infinity;

            if (_anchorPoints == null || _anchorPoints.Length == 0)
            {
                RefreshAnchorPointCache();
            }

            for (int i = 0; i < _anchorPoints.Length; i++)
            {
                AnchorPoint anchor = _anchorPoints[i];
                if (anchor == null || !anchor.isActiveAndEnabled)
                {
                    continue;
                }

                float distance = Vector3.Distance(position, anchor.transform.position);
                if (distance >= nearestDistance)
                {
                    continue;
                }

                nearestAnchor = anchor;
                nearestDistance = distance;
            }

            return nearestAnchor != null;
        }

        private void RefreshAnchorPointCache()
        {
            _anchorPoints = FindObjectsOfType<AnchorPoint>();
        }

        private MonoBehaviour ResolveHookHaptics()
        {
            if (hookHaptics == null)
            {
                hookHaptics = GetComponent(HapticAdapterTypeName) as MonoBehaviour;
            }

            if (hookHaptics == null)
            {
                hookHaptics = FindHapticAdapterInScene();
            }

            return hookHaptics;
        }

        private static MonoBehaviour FindHapticAdapterInScene()
        {
            MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour.GetType().Name == HapticAdapterTypeName)
                {
                    return behaviour;
                }
            }

            return null;
        }

        private static void SetHookHapticStrength(MonoBehaviour adapter, float strength)
        {
            adapter.SendMessage("SetStrength", strength, SendMessageOptions.DontRequireReceiver);
        }

        private void ClearHookHapticFeedback()
        {
            if (!_hookHapticsActive)
            {
                return;
            }

            MonoBehaviour adapter = ResolveHookHaptics();
            if (adapter != null)
            {
                SetHookHapticStrength(adapter, 0f);
            }

            _hookHapticsActive = false;
        }

        /// <summary>命中点更接近 A 球时返回 true；距离相同则稳定选择 A 球。</summary>
        private bool IsHitCloserToBallA(Vector3 hitPoint)
        {
            if (_ballA == null)
            {
                return false;
            }

            if (_ballB == null)
            {
                return true;
            }

            float distanceToA = (hitPoint - _ballA.position).sqrMagnitude;
            float distanceToB = (hitPoint - _ballB.position).sqrMagnitude;
            return distanceToA <= distanceToB;
        }

        /// <summary>
        /// 把被拖动球的目标位置夹紧到“以另一球为球心、maxBallDistance 为半径”的范围内，
        /// 使两球距离不超过上限。另一球缺失或约束关闭时原样返回。
        /// </summary>
        private Vector3 ClampToOther(Vector3 desired, Transform other)
        {
            if (!constrainBallDistance || other == null || maxBallDistance <= 0f)
            {
                return desired;
            }

            Vector3 anchor = other.position;
            Vector3 offset = desired - anchor;
            float distance = offset.magnitude;
            if (distance <= maxBallDistance)
            {
                return desired;
            }

            // 超距：沿从另一球指向目标点的方向，收回到最大距离处。
            return anchor + offset * (maxBallDistance / distance);
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

        /// <summary>收集本帧指针采样，保留按下/移动/松开事件用于绑定与结算小球。</summary>
        private void CollectPointerSamples(List<PointerSample> results)
        {
            results.Clear();

            if (Input.touchSupported && Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    PointerPhase phase = ConvertTouchPhase(touch.phase);
                    if (touch.phase == TouchPhase.Began)
                    {
                        if (IsPointerOverUi(touch.position, touch.fingerId))
                        {
                            _uiCapturedPointerIds.Add(touch.fingerId);
                        }
                        else
                        {
                            _uiCapturedPointerIds.Remove(touch.fingerId);
                        }
                    }

                    if (_uiCapturedPointerIds.Contains(touch.fingerId))
                    {
                        if (phase == PointerPhase.Ended)
                        {
                            _uiCapturedPointerIds.Remove(touch.fingerId);
                        }

                        continue;
                    }

                    results.Add(new PointerSample(touch.fingerId, touch.position, phase));
                }

                return;
            }

            const int MousePointerId = -1;
            bool mouseDown = Input.GetMouseButton(0);
            bool mouseBegan = Input.GetMouseButtonDown(0);
            bool mouseEnded = Input.GetMouseButtonUp(0);
            Vector2 mousePosition = Input.mousePosition;
            if (!mouseDown && !mouseBegan && !mouseEnded)
            {
                _mouseStartedOverUi = false;
            }

            if (mouseBegan)
            {
                _mouseStartedOverUi = IsPointerOverUi(mousePosition, MousePointerId);
            }

            if (_mouseStartedOverUi)
            {
                if (mouseEnded)
                {
                    _mouseStartedOverUi = false;
                }

                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                results.Add(new PointerSample(MousePointerId, mousePosition, PointerPhase.Began));
                return;
            }

            if (Input.GetMouseButton(0))
            {
                results.Add(new PointerSample(MousePointerId, mousePosition, PointerPhase.Moved));
                return;
            }

            if (Input.GetMouseButtonUp(0))
            {
                results.Add(new PointerSample(MousePointerId, mousePosition, PointerPhase.Ended));
            }
        }

        private bool IsPointerOverUi(Vector2 screenPosition, int pointerId)
        {
            if (!ignoreUiPointerInput || EventSystem.current == null)
            {
                return false;
            }

            return EventSystem.current.IsPointerOverGameObject(pointerId) ||
                RaycastUiAt(screenPosition, pointerId);
        }

        private bool RaycastUiAt(Vector2 screenPosition, int pointerId)
        {
            if (_uiPointerData == null)
            {
                _uiPointerData = new PointerEventData(EventSystem.current);
            }

            _uiPointerData.Reset();
            _uiPointerData.pointerId = pointerId;
            _uiPointerData.position = screenPosition;
            _uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(_uiPointerData, _uiRaycastResults);
            return _uiRaycastResults.Count > 0;
        }

        private PointerPhase ConvertTouchPhase(TouchPhase phase)
        {
            if (phase == TouchPhase.Began)
            {
                return PointerPhase.Began;
            }

            if (phase == TouchPhase.Ended || phase == TouchPhase.Canceled)
            {
                return PointerPhase.Ended;
            }

            return PointerPhase.Moved;
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
                renderer.enabled = ShouldShowBallVisuals();
            }

            return ballTransform;
        }

        private static bool ShouldShowBallVisuals()
        {
            LevelMgr levelMgr = LevelMgr.Instance;
            return levelMgr != null && levelMgr.IsDebug;
        }

        private Vector3 ResolveSpawnPosition(Transform spawnPoint, Vector3 fallbackPosition)
        {
            return spawnPoint != null ? spawnPoint.position : fallbackPosition;
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

            releaseMoveSpeed = Mathf.Max(0f, releaseMoveSpeed);
            releaseStandbyScreenDownOffset = Mathf.Max(0f, releaseStandbyScreenDownOffset);
            hookLoopVolume = Mathf.Clamp01(hookLoopVolume);
            hookHapticIntensityExponent = Mathf.Max(0.1f, hookHapticIntensityExponent);
            ResolveHookLoopClip();
        }

        private void UpdateHookLoopAudio()
        {
            bool shouldPlay = (_ballARuntime.State == ControllerBallState.Hooked ||
                               _ballBRuntime.State == ControllerBallState.Hooked) &&
                              ResolveHookLoopClip() != null;
            if (!shouldPlay)
            {
                StopHookLoopAudio();
                return;
            }

            AudioSource source = EnsureHookLoopSource();
            if (source.clip != hookLoopClip)
            {
                source.clip = hookLoopClip;
            }

            source.loop = true;
            source.playOnAwake = false;
            source.volume = hookLoopVolume;
            if (!source.isPlaying)
            {
                source.Play();
            }
        }

        private void StopHookLoopAudio()
        {
            if (_hookLoopSource != null && _hookLoopSource.isPlaying)
            {
                _hookLoopSource.Stop();
            }
        }

        private AudioSource EnsureHookLoopSource()
        {
            if (_hookLoopSource == null)
            {
                _hookLoopSource = gameObject.AddComponent<AudioSource>();
                _hookLoopSource.spatialBlend = 0f;
            }

            return _hookLoopSource;
        }

        private AudioClip ResolveHookLoopClip()
        {
#if UNITY_EDITOR
            if (hookLoopClip == null)
            {
                hookLoopClip = LoadEditorAudioClipAtPath("Assets/Art/Audio/摸索中.mp3");
            }
#endif
            return hookLoopClip;
        }

#if UNITY_EDITOR
        private static AudioClip LoadEditorAudioClipAtPath(string assetPath)
        {
            var assetDatabaseType = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            var loadMethod = assetDatabaseType != null
                ? assetDatabaseType.GetMethod("LoadAssetAtPath", new[] { typeof(string), typeof(Type) })
                : null;
            return loadMethod != null
                ? loadMethod.Invoke(null, new object[] { assetPath, typeof(AudioClip) }) as AudioClip
                : null;
        }
#endif

        private enum PointerPhase
        {
            Began,
            Moved,
            Ended
        }

        private readonly struct PointerSample
        {
            public readonly int PointerId;
            public readonly Vector2 ScreenPosition;
            public readonly PointerPhase Phase;

            public PointerSample(int pointerId, Vector2 screenPosition, PointerPhase phase)
            {
                PointerId = pointerId;
                ScreenPosition = screenPosition;
                Phase = phase;
            }
        }

        private struct BallRuntime
        {
            public ControllerBallState State;
            public int PointerId;
            public Vector3 ReleaseTarget;
            public bool HasReleaseTarget;

            public BallRuntime(ControllerBallState state)
            {
                State = state;
                PointerId = 0;
                ReleaseTarget = default;
                HasReleaseTarget = false;
            }

            public bool IsControlledBy(int pointerId)
            {
                return State == ControllerBallState.Hooked && PointerId == pointerId;
            }

            public void ClearPointer()
            {
                PointerId = 0;
            }
        }
    }
}
