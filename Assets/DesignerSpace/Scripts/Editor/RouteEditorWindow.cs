using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DesignerSpace.EditorTools
{
    /// <summary>
    /// 关卡动线编辑器。
    ///
    /// 策划打开本窗口后进入"打点模式"，在 Scene 视图的墙面（带 <see cref="Wall"/> 脚本的碰撞体）上点击即可打点；
    /// 连续打点会在上一个点与新点之间自动建立"先 -> 后"的有向边，用虚线箭头表示。
    /// 需要非顺序连接时切换到"连线模式"，依次点选两个已有节点即可建立有向边。
    /// 所有节点以场景 GameObject 形式存放在 RouteNetwork/RouteNodes 层级下，随场景保存，全程支持撤销。
    /// </summary>
    public class RouteEditorWindow : EditorWindow
    {
        private const string RouteNetworkName = "RouteNetwork";
        private const string RouteNodesContainerName = "RouteNodes";

        private enum EditMode
        {
            Off,
            Placing,
            Linking
        }

        private EditMode _mode = EditMode.Off;

        [SerializeField] private LayerMask _rayMask = ~0;
        [SerializeField] private float _hoverHeight = 0.02f;
        [SerializeField] private float _maxRayDistance = 500f;
        [SerializeField] private string _nodeNamePrefix = "RouteNode_";
        [SerializeField] private bool _autoChainWhilePlacing = true;

        private RouteNode _lastPlacedNode;
        private RouteNode _linkSourceNode;
        private int _nodeCounter;

        // 悬停状态缓存，供 Scene 提示使用。
        private bool _hoverValidWall;
        private Vector3 _hoverPoint;
        private Vector3 _hoverNormal;
        private bool _hasHover;

        private GUIStyle _hintStyle;

        [MenuItem("Window/DesignerSpace/关卡动线编辑器")]
        public static void Open()
        {
            RouteEditorWindow window = GetWindow<RouteEditorWindow>();
            window.titleContent = new GUIContent("关卡动线编辑器");
            window.minSize = new Vector2(320f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            _mode = EditMode.Off;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("动线编辑", EditorStyles.boldLabel);

            DrawModeButtons();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("参数", EditorStyles.boldLabel);
            _rayMask = LayerMaskField("射线检测层（粗过滤）", _rayMask);
            _hoverHeight = EditorGUILayout.FloatField(new GUIContent("法线偏移", "节点沿墙面法线方向抬起的距离，避免与墙面 Z-fighting"), _hoverHeight);
            _maxRayDistance = EditorGUILayout.FloatField(new GUIContent("射线最大距离"), _maxRayDistance);
            _autoChainWhilePlacing = EditorGUILayout.Toggle(new GUIContent("打点自动串联", "打点模式下自动把上一个点连向新点"), _autoChainWhilePlacing);
            _nodeNamePrefix = EditorGUILayout.TextField(new GUIContent("节点命名前缀"), _nodeNamePrefix);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("重置串联起点", "清除\"上一个点\"，下次打点将作为新链的起点")))
                {
                    _lastPlacedNode = null;
                }

                if (GUILayout.Button(new GUIContent("重置连线起点", "清除连线模式已选中的起点")))
                {
                    _linkSourceNode = null;
                }
            }

            if (GUILayout.Button(new GUIContent("为选中对象添加 Wall 脚本", "把选中的 GameObject 标记为可打点墙面")))
            {
                AddWallToSelection();
            }

            using (new EditorGUI.DisabledScope(FindRouteNetwork() == null))
            {
                if (GUILayout.Button(new GUIContent("清空当前动线网络", "删除 RouteNetwork 下所有节点")))
                {
                    ClearNetwork();
                }
            }

            EditorGUILayout.Space();
            DrawHelpBox();
        }

        private void DrawModeButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawModeToggle("打点模式", EditMode.Placing);
                DrawModeToggle("连线模式", EditMode.Linking);
            }

            string modeText;
            switch (_mode)
            {
                case EditMode.Placing:
                    modeText = "当前：打点模式（在墙面点击打点）";
                    break;
                case EditMode.Linking:
                    modeText = "当前：连线模式（依次点选两个节点）";
                    break;
                default:
                    modeText = "当前：已关闭（正常 Scene 操作）";
                    break;
            }

            EditorGUILayout.HelpBox(modeText, MessageType.None);
        }

        private void DrawModeToggle(string label, EditMode mode)
        {
            bool active = _mode == mode;
            bool next = GUILayout.Toggle(active, label, "Button", GUILayout.Height(28f));
            if (next == active)
            {
                return;
            }

            _mode = next ? mode : EditMode.Off;
            _lastPlacedNode = null;
            _linkSourceNode = null;
            SceneView.RepaintAll();
        }

        private void DrawHelpBox()
        {
            EditorGUILayout.HelpBox(
                "使用说明：\n" +
                "1. 墙面需挂 Wall 脚本才可打点（可用上方按钮为选中对象添加）。\n" +
                "2. 打点模式：在墙面点击逐个打点，相邻两点自动生成先->后虚线箭头。\n" +
                "3. 连线模式：先点起点节点，再点终点节点，生成有向箭头。\n" +
                "4. Ctrl+Z 可撤销；Ctrl+S 保存场景即持久化动线。",
                MessageType.Info);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_mode == EditMode.Off)
            {
                return;
            }

            Event evt = Event.current;

            if (_mode == EditMode.Placing)
            {
                HandlePlacingMode(evt);
            }
            else if (_mode == EditMode.Linking)
            {
                HandleLinkingMode(evt);
            }

            DrawSceneOverlay(sceneView);
        }

        private void HandlePlacingMode(Event evt)
        {
            // 抢占默认控制，避免打点时误触发框选/拖拽。
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            UpdateHover(evt);

            if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt)
            {
                if (_hoverValidWall)
                {
                    PlaceNode(_hoverPoint, _hoverNormal);
                }

                evt.Use();
            }

            SceneView.RepaintAll();
        }

        private void HandleLinkingMode(Event evt)
        {
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt)
            {
                RouteNode picked = PickRouteNode(evt.mousePosition);
                if (picked != null)
                {
                    HandleLinkPick(picked);
                }

                evt.Use();
            }

            SceneView.RepaintAll();
        }

        private void HandleLinkPick(RouteNode picked)
        {
            if (_linkSourceNode == null)
            {
                _linkSourceNode = picked;
                return;
            }

            if (_linkSourceNode == picked)
            {
                // 再次点同一个点视为取消选择。
                _linkSourceNode = null;
                return;
            }

            Undo.RecordObject(_linkSourceNode, "连接动线节点");
            bool added = _linkSourceNode.TryAddNext(picked);
            if (added)
            {
                EditorUtility.SetDirty(_linkSourceNode);
                MarkSceneDirty();
            }

            // 以刚连到的点作为下一段的起点，便于连续连线。
            _linkSourceNode = picked;
        }

        private void UpdateHover(Event evt)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
            _hasHover = false;
            _hoverValidWall = false;

            if (Physics.Raycast(ray, out RaycastHit hit, _maxRayDistance, _rayMask, QueryTriggerInteraction.Ignore))
            {
                _hasHover = true;
                _hoverPoint = hit.point;
                _hoverNormal = hit.normal;
                _hoverValidWall = hit.collider.GetComponentInParent<Wall>() != null;
            }
        }

        private void PlaceNode(Vector3 hitPoint, Vector3 normal)
        {
            Transform container = GetOrCreateNodesContainer();

            var go = new GameObject(_nodeNamePrefix + _nodeCounter.ToString("D2"));
            Undo.RegisterCreatedObjectUndo(go, "打点");
            _nodeCounter++;

            go.transform.SetParent(container, true);
            go.transform.position = hitPoint + normal.normalized * _hoverHeight;
            // 让节点朝向墙面外侧，便于后续需要方向时使用。
            if (normal.sqrMagnitude > Mathf.Epsilon)
            {
                go.transform.rotation = Quaternion.LookRotation(normal.normalized);
            }

            RouteNode node = Undo.AddComponent<RouteNode>(go);

            if (_autoChainWhilePlacing && _lastPlacedNode != null)
            {
                Undo.RecordObject(_lastPlacedNode, "连接动线节点");
                if (_lastPlacedNode.TryAddNext(node))
                {
                    EditorUtility.SetDirty(_lastPlacedNode);
                }
            }

            _lastPlacedNode = node;
            Selection.activeGameObject = go;
            EditorUtility.SetDirty(go);
            MarkSceneDirty();
        }

        private RouteNode PickRouteNode(Vector2 guiPosition)
        {
            GameObject picked = HandleUtility.PickGameObject(guiPosition, false);
            if (picked == null)
            {
                return null;
            }

            return picked.GetComponentInParent<RouteNode>();
        }

        private void DrawSceneOverlay(SceneView sceneView)
        {
            // 悬停高亮 / 提示。
            if (_mode == EditMode.Placing && _hasHover)
            {
                if (_hoverValidWall)
                {
                    Handles.color = new Color(0.2f, 0.85f, 1f, 1f);
                    Handles.DrawWireDisc(_hoverPoint, _hoverNormal, 0.15f);
                    Handles.color = new Color(0.2f, 0.85f, 1f, 0.4f);
                    Handles.DrawLine(_hoverPoint, _hoverPoint + _hoverNormal.normalized * 0.4f);
                }
                else
                {
                    Handles.color = new Color(1f, 0.35f, 0.25f, 1f);
                    Handles.DrawWireDisc(_hoverPoint, _hoverNormal.sqrMagnitude > 0f ? _hoverNormal : Vector3.up, 0.12f);
                }
            }

            // 连线模式下高亮已选起点。
            if (_mode == EditMode.Linking && _linkSourceNode != null)
            {
                Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
                Handles.DrawWireDisc(_linkSourceNode.transform.position, Vector3.up, _linkSourceNode.nodeRadius * 2f);
            }

            DrawSceneHint(sceneView);
        }

        private void DrawSceneHint(SceneView sceneView)
        {
            Handles.BeginGUI();

            if (_hintStyle == null)
            {
                _hintStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(8, 8, 4, 4)
                };
            }

            string hint;
            Color bg;
            if (_mode == EditMode.Placing)
            {
                if (_hasHover && !_hoverValidWall)
                {
                    hint = "当前不是可打点墙面（需带 Wall 脚本）";
                    bg = new Color(0.6f, 0.15f, 0.1f, 0.85f);
                }
                else
                {
                    hint = "打点模式：点击墙面打点";
                    bg = new Color(0.1f, 0.35f, 0.5f, 0.85f);
                }
            }
            else
            {
                hint = _linkSourceNode == null
                    ? "连线模式：点选起点节点"
                    : "连线模式：点选终点节点（再点起点取消）";
                bg = new Color(0.4f, 0.35f, 0.05f, 0.85f);
            }

            Vector2 size = _hintStyle.CalcSize(new GUIContent(hint));
            var rect = new Rect(12f, sceneView.position.height - size.y - 40f, size.x + 4f, size.y + 4f);

            Color prev = GUI.color;
            GUI.color = bg;
            GUI.DrawTexture(rect, EditorGUIUtility.whiteTexture);
            GUI.color = prev;
            GUI.Label(rect, hint, _hintStyle);

            Handles.EndGUI();
        }

        private Transform GetOrCreateNodesContainer()
        {
            GameObject network = FindRouteNetwork();
            if (network == null)
            {
                network = new GameObject(RouteNetworkName);
                Undo.RegisterCreatedObjectUndo(network, "创建动线网络");
            }

            Transform container = network.transform.Find(RouteNodesContainerName);
            if (container == null)
            {
                var containerGo = new GameObject(RouteNodesContainerName);
                Undo.RegisterCreatedObjectUndo(containerGo, "创建动线容器");
                containerGo.transform.SetParent(network.transform, false);
                container = containerGo.transform;
            }

            return container;
        }

        private static GameObject FindRouteNetwork()
        {
            GameObject network = GameObject.Find(RouteNetworkName);
            return network;
        }

        private void ClearNetwork()
        {
            GameObject network = FindRouteNetwork();
            if (network == null)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("清空动线网络", "确定删除 RouteNetwork 及其下所有动线节点？", "删除", "取消"))
            {
                return;
            }

            Undo.DestroyObjectImmediate(network);
            _lastPlacedNode = null;
            _linkSourceNode = null;
            _nodeCounter = 0;
            MarkSceneDirty();
        }

        private void AddWallToSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
            {
                EditorUtility.DisplayDialog("添加 Wall 脚本", "请先在 Hierarchy 中选中需要标记为墙面的对象。", "好的");
                return;
            }

            int added = 0;
            foreach (GameObject go in selection)
            {
                if (go.GetComponent<Wall>() != null)
                {
                    continue;
                }

                Undo.AddComponent<Wall>(go);
                added++;
            }

            if (added > 0)
            {
                MarkSceneDirty();
            }

            EditorUtility.DisplayDialog("添加 Wall 脚本", $"已为 {added} 个对象添加 Wall 脚本。", "好的");
        }

        private static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static LayerMask LayerMaskField(string label, LayerMask mask)
        {
            LayerMask tempMask = EditorGUILayout.MaskField(
                label,
                UnityEditorInternal.InternalEditorUtility.LayerMaskToConcatenatedLayersMask(mask),
                UnityEditorInternal.InternalEditorUtility.layers);
            return UnityEditorInternal.InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
        }
    }
}
