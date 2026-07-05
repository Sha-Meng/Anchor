using DesignerSpace;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// GameOver 场景：视频播放结束后展示本局游玩时长。
/// </summary>
public sealed class GameOverController : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private string videoAssetPath = "Assets/Art/Video/0f39e66983f85942ea10c231597b616f.mp4";

    private Canvas _canvas;
    private GameObject _resultPanel;
    private Text _timeText;
    private bool _resultShown;

    private void Awake()
    {
        if (videoPlayer == null)
        {
            videoPlayer = FindObjectOfType<VideoPlayer>();
        }

        EnsureVideoClip();
        BuildResultUi();
        HideResultPanel();

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached += OnVideoFinished;
            if (videoPlayer.clip == null)
            {
                OnVideoFinished(videoPlayer);
            }
        }
        else
        {
            ShowResultPanel();
        }
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
        }
    }

    private void EnsureVideoClip()
    {
        if (videoPlayer == null || videoPlayer.clip != null)
        {
            return;
        }

#if UNITY_EDITOR
        var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<VideoClip>(videoAssetPath);
        if (clip != null)
        {
            videoPlayer.clip = clip;
        }
#endif
    }

    private void OnVideoFinished(VideoPlayer source)
    {
        ShowResultPanel();
    }

    private void ShowResultPanel()
    {
        if (_resultShown)
        {
            return;
        }

        _resultShown = true;
        if (_resultPanel != null)
        {
            _resultPanel.SetActive(true);
        }

        if (_timeText != null)
        {
            _timeText.text = "用时 " + LevelSettlement.FormatElapsed(LevelSettlement.ElapsedSeconds);
        }
    }

    private void HideResultPanel()
    {
        if (_resultPanel != null)
        {
            _resultPanel.SetActive(false);
        }
    }

    private void BuildResultUi()
    {
        var canvasGo = new GameObject("GameOverCanvas");
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        canvasGo.AddComponent<GraphicRaycaster>();

        _resultPanel = new GameObject("ResultPanel", typeof(RectTransform));
        var panelRect = _resultPanel.GetComponent<RectTransform>();
        panelRect.SetParent(_canvas.transform, false);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var panelImage = _resultPanel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        var textGo = new GameObject("PlayTimeText", typeof(RectTransform));
        var textRect = textGo.GetComponent<RectTransform>();
        textRect.SetParent(panelRect, false);
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(900f, 220f);

        _timeText = textGo.AddComponent<Text>();
        _timeText.alignment = TextAnchor.MiddleCenter;
        _timeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _timeText.fontSize = 72;
        _timeText.color = Color.white;
        _timeText.text = "用时 00:00.00";
    }
}
