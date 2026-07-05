using System;
using System.Collections;
using System.Collections.Generic;
using Anchor.RivetRopeSystem;
using ClimbGame.Climb3C.Boot;
using ClimbGame.Climb3C.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Anchor.Networking
{
    public class AnchorNetworkDemoController : MonoBehaviour
    {
        private const string EntrySceneName = "NetworkDemoEntry";
        private const string MainLevelSceneName = "MainLevel";
        private const string MainLevel2SceneName = "MainLevel2";
        private const string MainLevelScenePath = "Assets/Scenes/MainLevel.scene";
        private const string MainLevel2ScenePath = "Assets/Scenes/MainLevel2.scene";

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
        private float _nextRemoteDebugLogTime;
        private readonly List<RoomListEntry> _availableRooms = new List<RoomListEntry>();
        private readonly List<RoomPlayerEntry> _roomPlayers = new List<RoomPlayerEntry>();
        private readonly HashSet<string> _handledEventIds = new HashSet<string>();
        private string _hostId;
        private string _remotePlayerId;
        private string _localSlot = "guest";
        private string _remoteSlot = "host";
        private string _localClimbRole = "second";
        private string _remoteClimbRole = "lead";
        private bool _gameSceneReady;
        private bool _gameSyncReady;
        private IClimbStateSource _localStateSource;
        private Climb3CLevelBinder _localClimbBinder;
        private AnchorRemoteClimbPlayer _remoteClimbPlayer;
        private RivetRopeNetworkBridge _rivetRopeBridge;
        private RivetRopeDebugDriver _rivetRopeDriver;
        private RivetRopeMainGameplayBinder _rivetRopeBinder;

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

        private struct RoomPlayerEntry
        {
            public string playerId;
            public string role;
            public bool isHost;
        }

        private struct AnchorStartPose
        {
            public Vector3 torso;
            public Vector3 leftHand;
            public Vector3 rightHand;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInstance();
            _instance.BuildForActiveScene();
        }

        public static void EnsureDemo()
        {
            EnsureInstance();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
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

            UpdateRemotePlayer();

            if (_gameSyncReady && _client != null && _client.IsConnected && Time.time >= _nextStateSendTime)
            {
                _nextStateSendTime = Time.time + _stateSendInterval;
                SendClimbState();
            }
        }

        private void BuildForActiveScene()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            ClearRuntimeSceneObjects();
            _currentView = null;

            if (IsGameSceneName(sceneName))
            {
                BuildGameScene();
            }
            else if (sceneName == EntrySceneName)
            {
                BuildEntryScene();
            }
            else
            {
                _inGame = false;
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
            AddText("RoleInfo", _isHost ? "你是房主 / 上方先锋攀。满 2 人后可开始。" : "你是非房主 / 下方第二攀登者，等待房主开始。", 15, TextAnchor.MiddleLeft, 30f);
            _startButton = AddButton("房主开始", StartRoom);
            AddButton("离开房间", LeaveRoom);
            _logText = AddText("Log", "房间日志：\n", 14, TextAnchor.UpperLeft, 160f);
            RefreshStartButton();
        }

        private void BuildGameScene()
        {
            BeginView("game");
            _inGame = true;
            _gameSceneReady = true;
            _gameSyncReady = false;
            ResolveRoomRoles();
            var sceneName = SceneManager.GetActiveScene().name;
            CreateCanvas("Anchor " + sceneName + " 联机");

            _statusText = AddText("Status", sceneName + " 等待同步确认", 20, TextAnchor.MiddleLeft);
            _logText = AddText("Log", GetRoomInfoText() + "\n游戏日志：\n", 15, TextAnchor.UpperLeft);

            DisableSceneSinglePlayerClimbBinders();
            BuildMainLevelPlayers();
            ConfigureRivetRopeNetworking(false);

            SendRoomEnteredGame();
        }

        private void DisableSceneSinglePlayerClimbBinders()
        {
            var binders = FindObjectsOfType<Climb3CLevelBinder>();
            for (int i = 0; i < binders.Length; i++)
            {
                var binder = binders[i];
                if (binder == null || binder == _localClimbBinder || IsUnderRuntimeRoot(binder.transform)) continue;

                binder.enabled = false;
                AppendLog("联机模式停用场景单人 Climb3C: " + binder.gameObject.name);
            }
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

        private void BuildMainLevelPlayers()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            var hostSpawn = _config.GetHostSpawn(sceneName);
            var guestSpawn = _config.GetGuestSpawn(sceneName);
            var localSpawn = _isHost ? hostSpawn : guestSpawn;
            var remoteSpawn = _isHost ? guestSpawn : hostSpawn;
            var localPose = ResolveAnchorStartPose(localSpawn, _isHost);
            var remotePose = ResolveAnchorStartPose(remoteSpawn, !_isHost);

            var binderGo = new GameObject("Anchor Local Climb Binder");
            ParentToRuntimeRoot(binderGo);
            _localClimbBinder = binderGo.AddComponent<Climb3CLevelBinder>();
            _localClimbBinder.useConfiguredStartCenter = true;
            _localClimbBinder.configuredStartCenter = localPose.torso;
            _localClimbBinder.configuredStartPointName = localSpawn.StartPointName;
            _localClimbBinder.characterFrontOffset = 2.35f;
            _localClimbBinder.bodyWallOffset = 2.35f;
            _localClimbBinder.useNearestStartAnchorPair = false;
            _localClimbBinder.primaryStartAnchorName = string.Empty;
            _localClimbBinder.leftHandStartAnchorName = localSpawn.LeftHandAnchorName;
            _localClimbBinder.rightHandStartAnchorName = localSpawn.RightHandAnchorName;
            _localClimbBinder.bodyColor = _isHost ? new Color(0.2f, 0.55f, 1f) : new Color(0.2f, 0.9f, 0.65f);
            _localClimbBinder.handColor = new Color(0.95f, 0.8f, 0.65f);

            _remoteClimbPlayer = CreateRemoteClimbPlayer(
                "Remote Climber " + (_remoteSlot == "host" ? "Lead" : "Second"),
                remotePose.torso,
                remotePose.leftHand,
                remotePose.rightHand,
                _isHost ? new Color(0.9f, 0.35f, 0.25f) : new Color(0.25f, 0.5f, 1f),
                new Color(0.95f, 0.8f, 0.65f));
            if (_remoteClimbPlayer.Root != null)
            {
                _remotePlayer = _remoteClimbPlayer.Root.gameObject;
                ParentToRuntimeRoot(_remotePlayer);
            }

            StartCoroutine(ResolveLocalStateSourceNextFrame());
            AppendLog("MainLevel 角色准备: 本地=" + _localSlot + "/" + _localClimbRole + FormatSpawnLog(localSpawn) + " 远端=" + _remoteSlot + "/" + _remoteClimbRole + FormatSpawnLog(remoteSpawn));
        }

        private void ConfigureRivetRopeNetworking(bool syncEnabled)
        {
            _rivetRopeDriver = FindObjectOfType<RivetRopeDebugDriver>();
            _rivetRopeBinder = FindObjectOfType<RivetRopeMainGameplayBinder>();

            if (_rivetRopeDriver == null)
            {
                AppendLog("铆钉同步未接入: 未找到 RivetRopeDebugDriver");
                return;
            }

            if (_rivetRopeBridge == null)
            {
                _rivetRopeBridge = GetComponent<RivetRopeNetworkBridge>();
                if (_rivetRopeBridge == null)
                {
                    _rivetRopeBridge = gameObject.AddComponent<RivetRopeNetworkBridge>();
                }
            }

            var localRole = _isHost ? RivetRopeLocalPlayerRole.Lead : RivetRopeLocalPlayerRole.Second;
            if (_rivetRopeBinder != null)
            {
                _rivetRopeBinder.SetLocalPlayerRole(localRole);
            }

            _rivetRopeBridge.Configure(_client, _rivetRopeDriver, _roomId, _playerId, syncEnabled);
            _rivetRopeDriver.SetNetworkSinkSource(_rivetRopeBridge);

            AppendLog("铆钉同步接入: 本地=" + localRole + " sync=" + syncEnabled);
        }

        private AnchorStartPose ResolveAnchorStartPose(AnchorSpawnSlotConfig spawn, bool hostFallback)
        {
            var leftAnchor = FindAnchorTransform(spawn?.LeftHandAnchorName);
            var rightAnchor = FindAnchorTransform(spawn?.RightHandAnchorName);
            var startPoint = FindStartPointTransform(spawn);
            var fallback = hostFallback ? new Vector3(0f, 3f, -0.95f) : new Vector3(0f, 1f, -0.95f);

            var left = leftAnchor != null ? leftAnchor.position : fallback + new Vector3(-0.45f, 0.25f, 0f);
            var right = rightAnchor != null ? rightAnchor.position : fallback + new Vector3(0.45f, 0.25f, 0f);
            var mid = (left + right) * 0.5f;
            var torso = startPoint != null ? startPoint.position : new Vector3(mid.x, mid.y - 0.35f, mid.z - 0.45f);

            return new AnchorStartPose
            {
                torso = torso,
                leftHand = left,
                rightHand = right
            };
        }

        private static Transform FindAnchorTransform(string anchorName)
        {
            if (string.IsNullOrEmpty(anchorName)) return null;

            var go = GameObject.Find(anchorName);
            return go != null ? go.transform : null;
        }

        private static Transform FindStartPointTransform(AnchorSpawnSlotConfig spawn)
        {
            if (spawn == null) return null;

            if (!string.IsNullOrEmpty(spawn.StartPointName))
            {
                var go = GameObject.Find(spawn.StartPointName);
                if (go != null) return go.transform;
            }

            var startPoints = FindObjectsOfType<StartPoint>();
            for (int i = 0; i < startPoints.Length; i++)
            {
                if (startPoints[i] != null && startPoints[i].MatchesSlot(spawn.slot))
                {
                    return startPoints[i].transform;
                }
            }

            return null;
        }

        private static string FormatSpawnLog(AnchorSpawnSlotConfig spawn)
        {
            if (spawn == null) return "(spawn=none)";

            return "(" + spawn.StartPointName + " L=" + spawn.LeftHandAnchorName + " R=" + spawn.RightHandAnchorName + ")";
        }

        private AnchorRemoteClimbPlayer CreateRemoteClimbPlayer(
            string name,
            Vector3 torso,
            Vector3 leftHand,
            Vector3 rightHand,
            Color bodyColor,
            Color handColor)
        {
            return new AnchorRemoteClimbPlayer(
                name,
                torso,
                leftHand,
                rightHand,
                bodyColor,
                handColor,
                ResolveRemoteCharacterSource(),
                ResolveRemoteInitialEuler(),
                ResolveRemoteCharacterScale(),
                ResolveRemoteCapsuleCenter(),
                ResolveRemoteCapsuleHeight(),
                ResolveRemoteCapsuleRadius());
        }

        private GameObject ResolveRemoteCharacterSource()
        {
            var sceneCharacterName = _localClimbBinder != null ? _localClimbBinder.sceneCharacterName : "RagDollMan";
            if (!string.IsNullOrEmpty(sceneCharacterName))
            {
                var sceneCharacter = GameObject.Find(sceneCharacterName);
                if (sceneCharacter != null) return sceneCharacter;
            }

            if (_localClimbBinder != null && _localClimbBinder.characterPrefab != null)
            {
                return _localClimbBinder.characterPrefab;
            }

#if UNITY_EDITOR
            var prefabPath = _localClimbBinder != null
                ? _localClimbBinder.characterPrefabPath
                : "Assets/Thridpart/PolyOne/FreeStickman/RagDollMan/PR_RagdollDemo_Mannequin.prefab";
            if (!string.IsNullOrEmpty(prefabPath))
            {
                return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
#endif

            return null;
        }

        private Vector3 ResolveRemoteInitialEuler()
        {
            return _localClimbBinder != null ? _localClimbBinder.initialFlatRotationEuler : new Vector3(90f, 0f, 0f);
        }

        private float ResolveRemoteCharacterScale()
        {
            return _localClimbBinder != null ? _localClimbBinder.characterScale : 1f;
        }

        private Vector3 ResolveRemoteCapsuleCenter()
        {
            return _localClimbBinder != null ? _localClimbBinder.capsuleCenter : Vector3.zero;
        }

        private float ResolveRemoteCapsuleHeight()
        {
            return _localClimbBinder != null ? _localClimbBinder.capsuleHeight : 1.6f;
        }

        private float ResolveRemoteCapsuleRadius()
        {
            return _localClimbBinder != null ? _localClimbBinder.capsuleRadius : 0.3f;
        }

        private IEnumerator ResolveLocalStateSourceNextFrame()
        {
            yield return null;
            _localStateSource = _localClimbBinder != null ? _localClimbBinder.StateSource : null;
            if (_localClimbBinder != null && _localClimbBinder.Controller != null)
            {
                _localClimbBinder.Controller.PlayerFailed -= HandleLocalPlayerFailed;
                _localClimbBinder.Controller.PlayerFailed += HandleLocalPlayerFailed;
            }

            if (_localStateSource == null)
            {
                AppendLog("警告: 未找到本地攀爬状态采样接口");
            }
        }

        private void HandleLocalPlayerFailed(PlayerHealthSnapshot snapshot, RopeFallResolution resolution)
        {
            if (!_gameSyncReady || string.IsNullOrEmpty(_roomId) || string.IsNullOrEmpty(_playerId))
            {
                return;
            }

            var eventSeq = ++_seq;
            var data = "{"
                + "\"health\":" + snapshot.CurrentHealth.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ","
                + "\"maxHealth\":" + snapshot.MaxHealth.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ","
                + "\"damage\":" + snapshot.LastDamage.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ","
                + AnchorJson.Pair("reason", snapshot.LastDamageReason) + ","
                + AnchorJson.Pair("firstProtectionRivetId", resolution.FirstProtectionRivetId) + ","
                + "\"firstProtectionSegmentLength\":" + resolution.FirstProtectionSegmentLength.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                + "}";
            var payload = AnchorJson.BuildClimbEventPayload(
                "player-failed-" + eventSeq.ToString("0000"),
                RivetRopeEventTypes.PlayerFailed,
                _playerId,
                data);

            Send(AnchorJson.BuildEnvelope(
                "game.event",
                roomId: _roomId,
                senderId: _playerId,
                seq: eventSeq,
                sentAt: Time.realtimeSinceStartup,
                schema: "climb-event.v1",
                payloadJson: payload));
            AppendLog("发送失败事件: damage=" + snapshot.LastDamage.ToString("0"));
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
            if (_remoteClimbPlayer != null)
            {
                _remoteClimbPlayer.Update(Time.deltaTime);
                return;
            }

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
            ResetRoomState();
            BuildRoomListView();
            RequestRoomList();
        }

        private void ReturnToEntry()
        {
            if (!string.IsNullOrEmpty(_roomId))
            {
                Send(AnchorJson.BuildEnvelope("room.leave", requestId: NewRequestId(), roomId: _roomId));
            }

            ResetRoomState();
            SceneManager.LoadScene(EntrySceneName);
        }

        private void ResetRoomState()
        {
            _roomId = null;
            _hostId = null;
            _remotePlayerId = null;
            _isHost = false;
            _inGame = false;
            _gameSceneReady = false;
            _gameSyncReady = false;
            _playerCount = 0;
            _localSlot = "guest";
            _remoteSlot = "host";
            _localClimbRole = "second";
            _remoteClimbRole = "lead";
            _roomPlayers.Clear();
            _handledEventIds.Clear();
            _availableRooms.Clear();
            _localStateSource = null;
            _localClimbBinder = null;
            _remoteClimbPlayer = null;
        }

        private void SendRoomEnteredGame()
        {
            Send(AnchorJson.BuildEnvelope("room.enteredGame", requestId: NewRequestId(), roomId: _roomId));
        }

        private void SendClimbState()
        {
            if (string.IsNullOrEmpty(_roomId) || string.IsNullOrEmpty(_playerId)) return;

            if (_localStateSource == null && _localClimbBinder != null)
            {
                _localStateSource = _localClimbBinder.StateSource;
            }

            if (_localStateSource == null || !_localStateSource.TryGetSnapshot(out var snapshot)) return;

            var payload = AnchorJson.BuildClimbStatePayload(_playerId, _localSlot, _localClimbRole, snapshot);
            Send(AnchorJson.BuildEnvelope(
                "game.state",
                roomId: _roomId,
                senderId: _playerId,
                seq: ++_seq,
                sentAt: Time.realtimeSinceStartup,
                schema: "climb-player-state.v1",
                payloadJson: payload));
        }

        private void HandleClimbState(string json)
        {
            var senderId = AnchorJson.GetString(json, "senderId");
            if (string.IsNullOrEmpty(senderId) || senderId == _playerId) return;

            var statePayload = AnchorJson.GetPayload(json);
            var seq = Mathf.RoundToInt(AnchorJson.GetFloat(json, "seq", 0f));
            var torso = AnchorJson.GetVector3(statePayload, "position", _remoteTarget);
            var left = AnchorJson.GetVector3(statePayload, "leftHandPosition", torso + new Vector3(-0.45f, 0.25f, 0f));
            var right = AnchorJson.GetVector3(statePayload, "rightHandPosition", torso + new Vector3(0.45f, 0.25f, 0f));
            var maxHealth = AnchorJson.GetFloat(statePayload, "maxHealth", 100f);
            var health = AnchorJson.GetFloat(statePayload, "health", maxHealth);
            var isFailed = AnchorJson.GetBool(statePayload, "isFailed");

            if (_remoteClimbPlayer == null)
            {
                var remoteSpawn = _isHost ? _config.GuestSpawn : _config.HostSpawn;
                var remotePose = ResolveAnchorStartPose(remoteSpawn, !_isHost);
                _remoteClimbPlayer = CreateRemoteClimbPlayer(
                    "Remote Climber Late",
                    remotePose.torso,
                    remotePose.leftHand,
                    remotePose.rightHand,
                    new Color(0.9f, 0.35f, 0.25f),
                    new Color(0.95f, 0.8f, 0.65f));
                if (_remoteClimbPlayer.Root != null)
                {
                    _remotePlayer = _remoteClimbPlayer.Root.gameObject;
                    ParentToRuntimeRoot(_remotePlayer);
                }
            }

            if (_remoteClimbPlayer.TryApplyState(seq, torso, left, right, health, maxHealth, isFailed))
            {
                _remoteTarget = torso;
                if (_logText != null && Time.time >= _nextRemoteDebugLogTime)
                {
                    _nextRemoteDebugLogTime = Time.time + 1f;
                    var sentAt = AnchorJson.GetFloat(json, "sentAt", Time.realtimeSinceStartup);
                    var latencyMs = Mathf.Max(0f, (Time.realtimeSinceStartup - sentAt) * 1000f);
                    AppendLog("远端状态: " + senderId + " seq=" + seq + " 延迟≈" + latencyMs.ToString("0") + "ms " + AnchorJson.GetString(statePayload, "movementState") + " HP=" + health.ToString("0"));
                }
            }
        }

        private void HandleClimbEvent(string payload)
        {
            var eventId = AnchorJson.GetString(payload, "eventId");
            if (!string.IsNullOrEmpty(eventId) && !_handledEventIds.Add(eventId)) return;

            var eventType = AnchorJson.GetString(payload, "eventType");
            if (eventType == RivetRopeEventTypes.PlayerFailed)
            {
                _remoteClimbPlayer?.MarkFailed();
            }

            AppendLog("收到 climb-event: " + eventType);
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
                    _hostId = AnchorJson.GetString(payload, "hostId") ?? _playerId;
                    _playerCount = 1;
                    _roomPlayers.Clear();
                    if (!string.IsNullOrEmpty(_playerId))
                    {
                        _roomPlayers.Add(new RoomPlayerEntry { playerId = _playerId, role = "host", isHost = true });
                    }
                    ResolveRoomRoles();
                    AppendLog("创建房间: " + _roomId);
                    BuildLobbyView();
                    break;
                case "room.joined":
                    _roomId = AnchorJson.GetString(json, "roomId") ?? AnchorJson.GetString(payload, "roomId");
                    _isHost = false;
                    _hostId = AnchorJson.GetString(payload, "hostId") ?? _hostId;
                    _playerCount = 2;
                    ResolveRoomRoles();
                    AppendLog("加入房间: " + _roomId);
                    BuildLobbyView();
                    break;
                case "room.updated":
                    ApplyRoomPayload(json, payload);
                    if (!string.IsNullOrEmpty(_roomId) && !IsGameSceneName(SceneManager.GetActiveScene().name)) BuildLobbyView();
                    break;
                case "room.starting":
                    AppendLog("收到开局，准备进入游戏场景");
                    StartCoroutine(LoadGameSceneAfterDelay(AnchorJson.GetFloat(payload, "countdownMs", 500f) / 1000f));
                    break;
                case "room.inGame":
                    AppendLog("房间进入游戏同步阶段");
                    _inGame = true;
                    _gameSyncReady = _gameSceneReady;
                    if (_gameSyncReady)
                    {
                        ConfigureRivetRopeNetworking(true);
                    }
                    break;
                case "room.peerLeft":
                    _remotePlayerId = AnchorJson.GetString(payload, "playerId") ?? _remotePlayerId;
                    if (_remoteClimbPlayer != null) _remoteClimbPlayer.MarkPeerLeft();
                    AppendLog("队友离开: " + _remotePlayerId);
                    break;
                case "game.state":
                    HandleClimbState(json);
                    break;
                case "game.event":
                    HandleClimbEvent(payload);
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
            var sceneName = ResolveBuildGameSceneName();
            if (string.IsNullOrEmpty(sceneName))
            {
                AppendLog("错误: 当前构建未包含 MainLevel 或 MainLevel2，无法进入多人主关卡");
                yield break;
            }

            AppendLog("进入 " + sceneName);
            SceneManager.LoadScene(sceneName);
        }

        private static bool IsGameSceneName(string sceneName)
        {
            return sceneName == MainLevelSceneName || sceneName == MainLevel2SceneName;
        }

        private static string ResolveBuildGameSceneName()
        {
            var hasMainLevel = IsSceneInBuild(MainLevelScenePath);
            var hasMainLevel2 = IsSceneInBuild(MainLevel2ScenePath);

            if (hasMainLevel) return MainLevelSceneName;
            if (hasMainLevel2) return MainLevel2SceneName;
            return null;
        }

        private static bool IsSceneInBuild(string scenePath)
        {
            return SceneUtility.GetBuildIndexByScenePath(scenePath) >= 0;
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
                : "房间：" + _roomId + " / " + _localSlot + " / " + (_isHost ? "上方先锋攀" : "下方第二攀登者") + " / " + _playerCount + "/2";
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

        private void ApplyRoomPayload(string json, string payload)
        {
            _roomId = AnchorJson.GetString(json, "roomId") ?? AnchorJson.GetString(payload, "roomId") ?? _roomId;
            _hostId = AnchorJson.GetString(payload, "hostId") ?? _hostId;
            _roomPlayers.Clear();
            _roomPlayers.AddRange(ParseRoomPlayers(payload));
            if (_roomPlayers.Count > 0)
            {
                _playerCount = _roomPlayers.Count;
            }
            else
            {
                _playerCount = Mathf.RoundToInt(AnchorJson.GetFloat(payload, "playerCount", _playerCount));
            }

            ResolveRoomRoles();
        }

        private List<RoomPlayerEntry> ParseRoomPlayers(string payload)
        {
            var result = new List<RoomPlayerEntry>();
            var playersRaw = AnchorJson.GetRawProperty(payload, "players");
            if (string.IsNullOrEmpty(playersRaw)) return result;

            foreach (var rawPlayer in AnchorJson.GetObjectArrayItems(playersRaw))
            {
                var playerId = AnchorJson.GetString(rawPlayer, "playerId");
                if (string.IsNullOrEmpty(playerId)) continue;

                result.Add(new RoomPlayerEntry
                {
                    playerId = playerId,
                    role = AnchorJson.GetString(rawPlayer, "role"),
                    isHost = AnchorJson.GetBool(rawPlayer, "isHost", playerId == _hostId)
                });
            }

            return result;
        }

        private void ResolveRoomRoles()
        {
            if (string.IsNullOrEmpty(_hostId) && _isHost)
            {
                _hostId = _playerId;
            }

            _isHost = !string.IsNullOrEmpty(_playerId) && _playerId == _hostId;
            _localSlot = _isHost ? "host" : "guest";
            _remoteSlot = _isHost ? "guest" : "host";
            _localClimbRole = _isHost ? "lead" : "second";
            _remoteClimbRole = _isHost ? "second" : "lead";
            _remotePlayerId = null;

            for (int i = 0; i < _roomPlayers.Count; i++)
            {
                if (_roomPlayers[i].playerId != _playerId)
                {
                    _remotePlayerId = _roomPlayers[i].playerId;
                    break;
                }
            }
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
            if (_rivetRopeBridge != null)
            {
                _rivetRopeBridge.SetSyncEnabled(false);
            }

            if (_runtimeRoot != null)
            {
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
            _localStateSource = null;
            _localClimbBinder = null;
            _remoteClimbPlayer = null;
            _rivetRopeDriver = null;
            _rivetRopeBinder = null;
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
                || go.name == "Remote Player"
                || go.name == "Anchor Local Climb Binder"
                || go.name.StartsWith("Remote Climber", StringComparison.Ordinal)
                || go.name == "Climber"
                || go.name == "Climb3C_Canvas"
                || go.name == "Climb3C_Services"
                || go.name == "Climb3C_Controller"
                || go.name == "Climb3C_RivetField"
                || go.name == "Climb3C_AnchorRegistry"
                || go.name == "HandMagnifier";
        }

        private bool IsUnderRuntimeRoot(Transform transform)
        {
            if (transform == null || _runtimeRoot == null) return false;

            for (var current = transform; current != null; current = current.parent)
            {
                if (current.gameObject == _runtimeRoot) return true;
            }

            return false;
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
            ParentToRuntimeRoot(eventSystem);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
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
