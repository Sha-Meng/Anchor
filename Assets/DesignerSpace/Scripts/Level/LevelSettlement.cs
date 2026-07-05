using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DesignerSpace
{
    public readonly struct SettlementTriggerContext
    {
        public AnchorPoint Anchor { get; }
        public string AnchorName { get; }
        public float ElapsedSeconds { get; }
        public bool FromNetwork { get; }

        public SettlementTriggerContext(AnchorPoint anchor, float elapsedSeconds, bool fromNetwork)
        {
            Anchor = anchor;
            AnchorName = anchor != null ? anchor.name : string.Empty;
            ElapsedSeconds = elapsedSeconds;
            FromNetwork = fromNetwork;
        }

        public SettlementTriggerContext(string anchorName, float elapsedSeconds, bool fromNetwork)
        {
            Anchor = null;
            AnchorName = anchorName ?? string.Empty;
            ElapsedSeconds = elapsedSeconds;
            FromNetwork = fromNetwork;
        }
    }

    /// <summary>
    /// 关卡结算与游玩计时服务。跨场景保留计时结果供 GameOver 展示。
    /// </summary>
    public sealed class LevelSettlement : MonoBehaviour
    {
        public const string GameOverSceneName = "GameOver";

        private static LevelSettlement _instance;

        private float _sessionStartTime = -1f;
        private float _elapsedSeconds;
        private bool _sessionActive;
        private bool _settled;

        public static event Action<SettlementTriggerContext> SettlementTriggered;

        public static float ElapsedSeconds => _instance != null ? _instance._elapsedSeconds : 0f;

        public static bool IsSettled => _instance != null && _instance._settled;

        public static bool IsSessionActive => _instance != null && _instance._sessionActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject(nameof(LevelSettlement));
            _instance = go.AddComponent<LevelSettlement>();
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
        }

        public static void BeginSession()
        {
            EnsureInstance();
            _instance._sessionActive = true;
            _instance._settled = false;
            _instance._sessionStartTime = Time.time;
            _instance._elapsedSeconds = 0f;
        }

        public static void ResetSession()
        {
            if (_instance == null)
            {
                return;
            }

            _instance._sessionActive = false;
            _instance._settled = false;
            _instance._sessionStartTime = -1f;
            _instance._elapsedSeconds = 0f;
        }

        public static bool RequestSettlement(AnchorPoint anchor)
        {
            EnsureInstance();
            if (_instance._settled || !SettlementAnchor.IsSettlementAnchor(anchor))
            {
                return false;
            }

            return _instance.FinalizeSettlement(anchor, fromNetwork: false);
        }

        public static bool RequestSettlementFromNetwork(float elapsedSeconds, string anchorName = null)
        {
            EnsureInstance();
            if (_instance._settled)
            {
                return false;
            }

            if (_instance._sessionActive && _instance._sessionStartTime >= 0f)
            {
                _instance._elapsedSeconds = Mathf.Max(0f, Time.time - _instance._sessionStartTime);
            }
            else
            {
                _instance._elapsedSeconds = Mathf.Max(0f, elapsedSeconds);
            }

            _instance._settled = true;
            _instance._sessionActive = false;

            SettlementTriggered?.Invoke(new SettlementTriggerContext(anchorName, _instance._elapsedSeconds, true));
            return true;
        }

        public static void LoadGameOver()
        {
            if (SceneManager.GetActiveScene().name == GameOverSceneName)
            {
                return;
            }

            SceneManager.LoadScene(GameOverSceneName);
        }

        public static string FormatElapsed(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            var span = TimeSpan.FromSeconds(seconds);
            return string.Format(
                "{0:00}:{1:00}.{2:00}",
                (int)span.TotalMinutes,
                span.Seconds,
                span.Milliseconds / 10);
        }

        private bool FinalizeSettlement(AnchorPoint anchor, bool fromNetwork)
        {
            if (_settled)
            {
                return false;
            }

            _settled = true;
            _sessionActive = false;
            _elapsedSeconds = fromNetwork
                ? _elapsedSeconds
                : Mathf.Max(0f, Time.time - _sessionStartTime);

            SettlementTriggered?.Invoke(new SettlementTriggerContext(anchor, _elapsedSeconds, fromNetwork));
            LoadGameOver();
            return true;
        }
    }
}
