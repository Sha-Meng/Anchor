using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Anchor.Networking
{
    public class AnchorNetworkDemoController : MonoBehaviour
    {
        private const string EntrySceneName = "NetworkDemoEntry";
        private const string GameSceneName = "NetworkDemoGame";

        private static AnchorNetworkDemoController _instance;

        private AnchorRelayClient _client;
        private AnchorProtocolConfig _config;
        private string _clientId;
        private string _playerId;
        private string _roomId;
        private bool _isHost;
        private bool _inGame;
        private bool _helloSent;
        private bool _autoConnectAttempted;
        private int _playerCount;
        private int _seq;
        private float _nextStateSendTime;
        private float _stateSendInterval = 0.1f;
        private readonly List<RoomListEntry> _availableRooms = new List<RoomListEntry>();

        private Canvas _canvas;
        private GameObject _runtimeRoot;
        private Text _statusText;
        private Text _logText;
        private Text _roomText;
        private InputField _endpointInput;
        private InputField _roomInput;
        private Button _startButton;
        private GameObject _localPlayer;
        private GameObject _remotePlayer;
        private Vector3 _remoteTarget = new Vector3(2f, 0.5f, 0f);
        private float _nextUiY = -58f;
        private string _currentView;

        private struct RoomListEntry
        {
            public string roomId;
            public string state;
            public int playerCount;
            public int maxPlayers;
            public bool canJoin;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            _instance.BuildForActiveScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInstance();
            _instance.BuildForActiveScene();
        }

        public static void EnsureDemo()
        {
            EnsureInstance();
            _instance.BuildForActiveScene();
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;

            var go = new GameObject("Anchor Network Demo Controller");
            _instance = go.AddComponent<AnchorNetworkDemoController>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            _config = AnchorProtocolConfig.Load();
            _stateSendInterval = Mathf.Max(0.02f, _config.DemoStateSendInterval);
            _clientId = AnchorClientIdentity.GetOrCreateClientId();
            _client = gameObject.AddComponent<AnchorRelayClient>();
            _client.MessageReceived += HandleMessage;
            _client.StatusChanged += AppendLog;
        }

        private void Update()
        {
            if (!_inGame) return;

            UpdateLocalPlayer();
            UpdateRemotePlayer();

            if (_client != null && _client.IsConnected && Time.time >= _nextStateSendTime)
            {
                _nextStateSendTime = Time.time + _stateSendInterval;
                SendDemoState();
            }
        }

        private void BuildForActiveScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            ClearRuntimeSceneObjects();
            _currentView = null;

            if (sceneName == GameSceneName)
            {
                BuildGameScene();
            }
            else
            {
                BuildEntryScene();
            }
        }

        private void BuildEntryScene()
        {
            _inGame = false;
            if (string.IsNullOrEmpty(_roomId))
            {
                BuildRoomListView();
            }
            else
            {
                BuildLobbyView();
            }
        }

        private void BuildRoomListView()
        {
            BeginView("list", true);
            EnsureEntryCamera();
            CreateCanvas("Anchor Network Demo - 房间列表");

            _statusText = AddText("Status", "状态：未连接", 16, TextAnchor.MiddleLeft);
            _endpointInput = AddInput("EndpointInput", _config.DefaultEndpoint);
            AddButton(_client != null && _client.IsConnected ? "已连接" : "连接", Connect);
            AddButton("刷新房间列表", RequestRoomList);
            AddButton("创建房间", CreateRoom);

            if (_availableRooms.Count == 0)
            {
                AddText("RoomListEmpty", "当前无房间。创建房间后，另一个客户端刷新列表即可看到。", 15, TextAnchor.MiddleLeft, 34f);
            }
            else
            {
                AddText("RoomListTitle", "当前房间：", 15, TextAnchor.MiddleLeft, 24f);
                foreach (var room in _availableRooms)
                {
                    var label = $"{room.roomId}  {room.playerCount}/{room.maxPlayers}  {FormatRoomState(room.state)}";
                    if (room.canJoin)
                    {
                        var targetRoomId = room.roomId;
                        AddButton("加入 " + label, () => JoinRoom(targetRoomId));
                    }
                    else
                    {
                        AddText("Room_" + room.roomId, label + "（不可加入）", 14, TextAnchor.MiddleLeft, 26f);
                    }
                }
            }

            _roomInput = AddInput("RoomInput", string.Empty);
            AddButton("按房间码加入", JoinRoomFromInput);
            _logText = AddText("Log", "日志：\n", 14, TextAnchor.UpperLeft, 116f);

            if (!_autoConnectAttempted)
            {
                _autoConnectAttempted = true;
                StartCoroutine(AutoConnectNextFrame());
            }
        }

        private void BuildLobbyView()
        {
            if (_currentView == "lobby" && _canvas != null)
            {
                RefreshRoomInfo();
                return;
            }
            BeginView("lobby");
            EnsureEntryCamera();
            CreateCanvas("Anchor Network Demo - 房间");

            _statusText = AddText("Status", "状态：房间中", 16, TextAnchor.MiddleLeft);
            _roomText = AddText("RoomInfo", GetRoomInfoText(), 16, TextAnchor.MiddleLeft, 34f);
            AddText("RoleInfo", _isHost ? "你是房主。满 2 人后可开始。" : "你是访客，等待房主开始。", 15, TextAnchor.MiddleLeft, 30f);
            _startButton = AddButton("房主开始", StartRoom);
            AddButton("离开房间", LeaveRoom);
            _logText = AddText("Log", "房间日志：\n", 14, TextAnchor.UpperLeft, 160f);
            RefreshStartButton();
        }

        private void BuildGameScene()
        {
            BeginView("game");
            _inGame = true;
            CreateCanvas("Anchor Network Demo Game");

            _statusText = AddText("Status", "Demo Game", 20, TextAnchor.MiddleLeft);
            AddButton("发送自定义 game.event", SendDemoEvent);
            AddButton("返回房间入口", ReturnToEntry);
            _logText = AddText("Log", "游戏日志：\n", 15, TextAnchor.UpperLeft);

            var cameraGo = new GameObject("Demo Camera");
            ParentToRuntimeRoot(cameraGo);
            var camera = cameraGo.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 6f, -8f);
            camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
            cameraGo.tag = "MainCamera";

            var lightGo = new GameObject("Demo Directional Light");
            ParentToRuntimeRoot(lightGo);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Demo Floor";
            ParentToRuntimeRoot(floor);
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.1f, 5f);
            floor.GetComponent<Renderer>().material.color = new Color(0.2f, 0.22f, 0.25f);

            _localPlayer = CreatePlayerCube("Local Player", new Vector3(-2f, 0.5f, 0f), Color.cyan);
            _remotePlayer = CreatePlayerCube("Remote Player", _remoteTarget, Color.red);

            SendRoomEnteredGame();
        }

        private GameObject CreatePlayerCube(string name, Vector3 position, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            ParentToRuntimeRoot(go);
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            go.GetComponent<Renderer>().material.color = color;
            return go;
        }

        private void UpdateLocalPlayer()
        {
            if (_localPlayer == null) return;

            var move = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            if (move.sqrMagnitude > 1f) move.Normalize();
            _localPlayer.transform.position += move * (Time.deltaTime * 3f);
            if (move.sqrMagnitude > 0.01f)
            {
                _localPlayer.transform.rotation = Quaternion.LookRotation(move);
            }
        }

        private void UpdateRemotePlayer()
        {
            if (_remotePlayer == null) return;
            _remotePlayer.transform.position = Vector3.Lerp(_remotePlayer.transform.position, _remoteTarget, Time.deltaTime * 10f);
        }

        private void Connect()
        {
            if (_client == null) return;
            if (_client.IsConnected)
            {
                RequestRoomList();
                return;
            }
            _helloSent = false;
            _client.Connect(_endpointInput != null ? _endpointInput.text : _config.DefaultEndpoint);
        }

        private IEnumerator AutoConnectNextFrame()
        {
            yield return null;
            if (_client != null && !_client.IsConnected) Connect();
        }

        private void RequestRoomList()
        {
            Send(AnchorJson.BuildEnvelope("room.list", requestId: NewRequestId()));
        }

        private void CreateRoom()
        {
            Send(AnchorJson.BuildEnvelope("room.create", requestId: NewRequestId()));
        }

        private void JoinRoomFromInput()
        {
            var roomId = _roomInput != null ? _roomInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(roomId))
            {
                AppendLog("请输入房间码，或点击列表中的可加入房间");
                return;
            }

            JoinRoom(roomId);
        }

        private void JoinRoom(string roomId)
        {
            Send(AnchorJson.BuildEnvelope("room.join", requestId: NewRequestId(), roomId: roomId));
        }

        private void StartRoom()
        {
            Send(AnchorJson.BuildEnvelope("room.start", requestId: NewRequestId(), roomId: _roomId));
        }

        private void LeaveRoom()
        {
            Send(AnchorJson.BuildEnvelope("room.leave", requestId: NewRequestId(), roomId: _roomId));
            _roomId = null;
            _isHost = false;
            _playerCount = 0;
            _availableRooms.Clear();
            BuildRoomListView();
            RequestRoomList();
        }

        private void ReturnToEntry()
        {
            _inGame = false;
            SceneManager.LoadScene(EntrySceneName);
        }

        private void SendRoomEnteredGame()
        {
            Send(AnchorJson.BuildEnvelope("room.enteredGame", requestId: NewRequestId(), roomId: _roomId));
        }

        private void SendDemoState()
        {
            if (_localPlayer == null || string.IsNullOrEmpty(_roomId)) return;

            var payload = AnchorJson.BuildDemoStatePayload(
                _localPlayer.transform.position,
                _localPlayer.transform.eulerAngles.y,
                "demo-moving");

            Send(AnchorJson.BuildEnvelope(
                "game.state",
                roomId: _roomId,
                senderId: _playerId,
                seq: ++_seq,
                sentAt: Time.realtimeSinceStartup,
                schema: "demo-state.v1",
                payloadJson: payload));
        }

        private void SendDemoEvent()
        {
            if (string.IsNullOrEmpty(_roomId)) return;

            var payload = AnchorJson.BuildDemoEventPayload(Guid.NewGuid().ToString("N"), "demo.ping", "Hello from " + _playerId);
            Send(AnchorJson.BuildEnvelope(
                "game.event",
                roomId: _roomId,
                senderId: _playerId,
                seq: ++_seq,
                sentAt: Time.realtimeSinceStartup,
                schema: "demo-event.v1",
                payloadJson: payload));
            AppendLog("已发送自定义 game.event");
        }

        private void Send(string json)
        {
            if (_client == null || !_client.IsConnected)
            {
                AppendLog("未连接 relay");
                return;
            }

            _client.SendText(json);
        }

        private void HandleMessage(string json)
        {
            var type = AnchorJson.GetString(json, "type");
            var payload = AnchorJson.GetPayload(json);

            switch (type)
            {
                case "system.welcome":
                    _playerId = AnchorJson.GetString(payload, "playerId");
                    AppendLog("欢迎: playerId=" + _playerId);
                    if (!_helloSent)
                    {
                        _helloSent = true;
                        Send(AnchorJson.BuildEnvelope(
                            "system.hello",
                            requestId: NewRequestId(),
                            payloadJson: "{\"clientId\":" + AnchorJson.Quote(_clientId) + ",\"clientVersion\":\"mvp\"}"));
                    }
                    RequestRoomList();
                    break;
                case "room.list.result":
                case "room.list.updated":
                    _availableRooms.Clear();
                    _availableRooms.AddRange(ParseRooms(payload));
                    AppendLog("可加入房间数: " + _availableRooms.Count);
                    if (string.IsNullOrEmpty(_roomId)) BuildRoomListView();
                    break;
                case "room.created":
                    _roomId = AnchorJson.GetString(json, "roomId") ?? AnchorJson.GetString(payload, "roomId");
                    _isHost = true;
                    _playerCount = 1;
                    AppendLog("创建房间: " + _roomId);
                    BuildLobbyView();
                    break;
                case "room.joined":
                    _roomId = AnchorJson.GetString(json, "roomId") ?? AnchorJson.GetString(payload, "roomId");
                    _isHost = false;
                    _playerCount = 2;
                    AppendLog("加入房间: " + _roomId);
                    BuildLobbyView();
                    break;
                case "room.updated":
                    _roomId = AnchorJson.GetString(json, "roomId") ?? AnchorJson.GetString(payload, "roomId") ?? _roomId;
                    _isHost = AnchorJson.GetString(payload, "hostId") == _playerId;
                    _playerCount = Mathf.RoundToInt(AnchorJson.GetFloat(payload, "playerCount", _playerCount));
                    if (!string.IsNullOrEmpty(_roomId) && SceneManager.GetActiveScene().name != GameSceneName) BuildLobbyView();
                    break;
                case "room.starting":
                    AppendLog("收到开局，进入 Demo Game");
                    StartCoroutine(LoadGameSceneAfterDelay(AnchorJson.GetFloat(payload, "countdownMs", 500f) / 1000f));
                    break;
                case "room.inGame":
                    AppendLog("房间进入游戏同步阶段");
                    _inGame = true;
                    break;
                case "room.peerLeft":
                    AppendLog("队友离开");
                    break;
                case "game.state":
                    var statePayload = AnchorJson.GetPayload(json);
                    _remoteTarget = AnchorJson.GetVector3(statePayload, "position", _remoteTarget);
                    break;
                case "game.event":
                    AppendLog("收到自定义 game.event: " + AnchorJson.GetString(payload, "eventType"));
                    break;
                case "system.error":
                    AppendLog("错误: " + AnchorJson.GetString(payload, "code") + " " + AnchorJson.GetString(payload, "message"));
                    break;
                default:
                    AppendLog("收到消息: " + type);
                    break;
            }
        }

        private IEnumerator LoadGameSceneAfterDelay(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            SceneManager.LoadScene(GameSceneName);
        }

        private void RefreshRoomInfo()
        {
            if (_roomText != null)
            {
                _roomText.text = GetRoomInfoText();
            }

            RefreshStartButton();
        }

        private void RefreshStartButton()
        {
            if (_startButton != null) _startButton.interactable = _isHost && !string.IsNullOrEmpty(_roomId) && _playerCount >= 2;
        }

        private string GetRoomInfoText()
        {
            return string.IsNullOrEmpty(_roomId)
                ? "房间：无"
                : "房间：" + _roomId + " / " + (_isHost ? "host" : "guest") + " / " + _playerCount + "/2";
        }

        private string FormatRoomState(string state)
        {
            switch (state)
            {
                case "lobby":
                    return "可加入";
                case "starting":
                    return "开局中";
                case "inGame":
                    return "游戏中";
                default:
                    return string.IsNullOrEmpty(state) ? "未知" : state;
            }
        }

        private List<RoomListEntry> ParseRooms(string payload)
        {
            var result = new List<RoomListEntry>();
            var roomsRaw = AnchorJson.GetRawProperty(payload, "rooms");
            if (string.IsNullOrEmpty(roomsRaw)) return result;

            foreach (var rawRoom in AnchorJson.GetObjectArrayItems(roomsRaw))
            {
                var roomId = AnchorJson.GetString(rawRoom, "roomId");
                if (string.IsNullOrEmpty(roomId)) continue;

                var state = AnchorJson.GetString(rawRoom, "state");
                var playerCount = Mathf.RoundToInt(AnchorJson.GetFloat(rawRoom, "playerCount", 0f));
                var maxPlayers = Mathf.RoundToInt(AnchorJson.GetFloat(rawRoom, "maxPlayers", 2f));
                var canJoinByRoomState = state == "lobby" && playerCount < maxPlayers;

                result.Add(new RoomListEntry
                {
                    roomId = roomId,
                    state = state,
                    playerCount = playerCount,
                    maxPlayers = maxPlayers,
                    canJoin = AnchorJson.GetBool(rawRoom, "canJoin", canJoinByRoomState) || canJoinByRoomState
                });
            }

            return result;
        }

        private string NewRequestId()
        {
            return "req-" + (++_seq).ToString("0000");
        }

        private void AppendLog(string message)
        {
            Debug.Log("[AnchorNetworkDemo] " + message);
            if (_statusText != null) _statusText.text = "状态：" + message;
            if (_logText != null) _logText.text = TrimLog(_logText.text + "\n" + message);
        }

        private string TrimLog(string value)
        {
            const int maxLength = 1800;
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value;
            return value.Substring(value.Length - maxLength);
        }

        private void ClearRuntimeSceneObjects()
        {
            if (_runtimeRoot != null)
            {
                DetachEventSystems(_runtimeRoot.transform);
                DestroyImmediate(_runtimeRoot);
                _runtimeRoot = null;
            }

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (!IsDemoRuntimeObject(go)) continue;
                DestroyImmediate(go);
            }

            _canvas = null;
            _statusText = null;
            _logText = null;
            _roomText = null;
            _endpointInput = null;
            _roomInput = null;
            _startButton = null;
            _localPlayer = null;
            _remotePlayer = null;
        }

        private void BeginView(string view, bool forceRebuild = false)
        {
            if (!forceRebuild && _currentView == view && _canvas != null) return;
            ClearRuntimeSceneObjects();
            _runtimeRoot = new GameObject("Anchor Demo Runtime Root");
            _currentView = view;
        }

        private bool IsDemoRuntimeObject(GameObject go)
        {
            if (go.GetComponent<EventSystem>() != null || go.GetComponent<StandaloneInputModule>() != null) return false;

            return go.name.StartsWith("Anchor Demo ", StringComparison.Ordinal)
                || go.name.StartsWith("Demo ", StringComparison.Ordinal)
                || go.name == "Anchor Demo Canvas"
                || go.name == "Anchor Demo Runtime Root"
                || go.name == "Local Player"
                || go.name == "Remote Player";
        }

        private void DetachEventSystems(Transform root)
        {
            foreach (var eventSystem in root.GetComponentsInChildren<EventSystem>(true))
            {
                eventSystem.transform.SetParent(null, false);
                DontDestroyOnLoad(eventSystem.gameObject);
            }
        }

        private void CreateCanvas(string title)
        {
            EnsureEventSystem();
            _nextUiY = -58f;

            var root = new GameObject("Anchor Demo Canvas");
            ParentToRuntimeRoot(root);
            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();

            var titleText = AddText("Title", title, 24, TextAnchor.MiddleCenter, 36f);
            var rect = titleText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -10f);
            rect.sizeDelta = new Vector2(0f, 40f);
            _nextUiY = -58f;
        }

        private void EnsureEntryCamera()
        {
            if (Camera.main != null) return;

            var cameraGo = new GameObject("Demo Entry Camera");
            ParentToRuntimeRoot(cameraGo);
            var camera = cameraGo.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            cameraGo.tag = "MainCamera";
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;

            var eventSystem = new GameObject("Anchor Demo EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(eventSystem);
        }

        private void ParentToRuntimeRoot(GameObject go)
        {
            if (_runtimeRoot == null)
            {
                _runtimeRoot = new GameObject("Anchor Demo Runtime Root");
            }

            go.transform.SetParent(_runtimeRoot.transform, false);
        }

        private Text AddText(string name, string text, int fontSize, TextAnchor alignment, float height = 28f)
        {
            var go = new GameObject("Anchor Demo " + name);
            go.transform.SetParent(_canvas.transform, false);
            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = alignment;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, _nextUiY);
            rect.sizeDelta = new Vector2(-40f, height);
            _nextUiY -= height + 6f;
            return label;
        }

        private InputField AddInput(string name, string text)
        {
            var go = new GameObject("Anchor Demo " + name);
            go.transform.SetParent(_canvas.transform, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
            var input = go.AddComponent<InputField>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var inputText = textGo.AddComponent<Text>();
            inputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            inputText.fontSize = 16;
            inputText.color = Color.white;
            inputText.alignment = TextAnchor.MiddleLeft;
            input.textComponent = inputText;
            input.text = text;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 0f);
            textRect.offsetMax = new Vector2(-10f, 0f);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, _nextUiY);
            rect.sizeDelta = new Vector2(-40f, 30f);
            _nextUiY -= 36f;
            return input;
        }

        private Button AddButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Anchor Demo Button " + label);
            go.transform.SetParent(_canvas.transform, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.35f, 0.65f, 0.95f);
            var button = go.AddComponent<Button>();
            button.onClick.AddListener(onClick);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = 16;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, _nextUiY);
            rect.sizeDelta = new Vector2(-40f, 30f);
            _nextUiY -= 36f;
            return button;
        }
    }
}
