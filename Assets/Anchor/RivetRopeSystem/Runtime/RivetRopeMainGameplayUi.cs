using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Anchor.RivetRopeSystem
{
    public sealed class RivetRopeMainGameplayUi : MonoBehaviour
    {
        [SerializeField] private RivetRopeDebugDriver driver;
        [SerializeField] private RivetRopeMainGameplayBinder binder;
        [SerializeField] private Vector2 panelAnchor = new Vector2(1f, 0.5f);
        [SerializeField] private Vector2 panelPivot = new Vector2(1f, 0.5f);
        [SerializeField] private Vector2 panelOffset = new Vector2(-32f, 0f);
        [SerializeField] private bool showStatus = true;
        [SerializeField] private bool showLookButtons = true;
        [SerializeField] private int lookUpPoseIndex = 3;
        [SerializeField] private int lookDownPoseIndex = 4;

        private Canvas _canvas;
        private GameObject _panelObject;
        private RectTransform _panelRect;
        private Text _statusText;
        private Button _placeButton;
        private Button _collectButton;
        private Button _switchButton;
        private Button _rescueButton;
        private Button _lookUpButton;
        private Button _lookDownButton;
        private Component _cameraMgr;
        private int _heldLookPoseIndex = -1;
        private int _restoreLookPoseIndex = -1;

        private const string CameraMgrTypeName = "DesignerSpace.CameraMgr";

        private void Awake()
        {
            BuildUi();
        }

        private void Update()
        {
            RefreshState();
            ApplyHeldLookPose();
        }

        private void BuildUi()
        {
            EnsureEventSystem();

            var canvasObject = new GameObject("Rivet Rope Gameplay Canvas");
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            canvasObject.AddComponent<GraphicRaycaster>();

            var panel = new GameObject("Rivet Rope Actions");
            _panelObject = panel;
            panel.transform.SetParent(canvasObject.transform, false);
            var rect = panel.AddComponent<RectTransform>();
            _panelRect = rect;
            rect.anchorMin = panelAnchor;
            rect.anchorMax = panelAnchor;
            rect.pivot = panelPivot;
            rect.anchoredPosition = panelOffset;
            rect.sizeDelta = new Vector2(260f, showStatus ? 230f : 170f);

            var image = panel.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.35f);

            _placeButton = AddButton(panel.transform, "插锚", new Vector2(0f, -20f), OnPlaceClicked);
            _collectButton = AddButton(panel.transform, "回收铆钉", new Vector2(0f, -74f), OnCollectClicked);
            _switchButton = AddButton(panel.transform, "换领", new Vector2(0f, -128f), OnSwitchClicked);
            _rescueButton = AddButton(panel.transform, "收绳救援", new Vector2(0f, -182f), OnRescueClicked);
            _lookUpButton = AddButton(panel.transform, "向上看", new Vector2(0f, -236f), null);
            _lookDownButton = AddButton(panel.transform, "向下看", new Vector2(0f, -290f), null);
            AddHoldEvents(_lookUpButton, () => BeginLook(lookUpPoseIndex), EndLook);
            AddHoldEvents(_lookDownButton, () => BeginLook(lookDownPoseIndex), EndLook);

            if (showStatus)
            {
                var statusObject = new GameObject("Status");
                statusObject.transform.SetParent(panel.transform, false);
                _statusText = statusObject.AddComponent<Text>();
                _statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _statusText.fontSize = 17;
                _statusText.alignment = TextAnchor.UpperLeft;
                _statusText.color = Color.white;
                var statusRect = _statusText.rectTransform;
                statusRect.anchorMin = new Vector2(0f, 1f);
                statusRect.anchorMax = new Vector2(1f, 1f);
                statusRect.pivot = new Vector2(0.5f, 1f);
                statusRect.anchoredPosition = new Vector2(0f, -232f);
                statusRect.sizeDelta = new Vector2(-24f, 68f);
            }
        }

        private Button AddButton(Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
        {
            var buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.12f, 0.12f, 0.12f, 0.82f);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(-28f, 46f);

            var textObject = new GameObject("Label");
            textObject.transform.SetParent(buttonObject.transform, false);
            var text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        private static void AddHoldEvents(Button button, UnityEngine.Events.UnityAction onPress, UnityEngine.Events.UnityAction onRelease)
        {
            if (button == null)
            {
                return;
            }

            var trigger = button.gameObject.AddComponent<EventTrigger>();
            AddEventTrigger(trigger, EventTriggerType.PointerDown, onPress);
            AddEventTrigger(trigger, EventTriggerType.PointerUp, onRelease);
            AddEventTrigger(trigger, EventTriggerType.PointerExit, onRelease);
            AddEventTrigger(trigger, EventTriggerType.Cancel, onRelease);
        }

        private static void AddEventTrigger(EventTrigger trigger, EventTriggerType eventId, UnityEngine.Events.UnityAction action)
        {
            if (trigger == null || action == null)
            {
                return;
            }

            var entry = new EventTrigger.Entry { eventID = eventId };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        private void RefreshState()
        {
            var hasGameplay = driver != null && binder != null;
            var showLook = showLookButtons && ResolveCameraMgr() != null;

            if (!hasGameplay)
            {
                SetVisible(_placeButton, false);
                SetVisible(_collectButton, false);
                SetVisible(_switchButton, false);
                SetVisible(_rescueButton, false);
                SetVisible(_lookUpButton, showLook);
                SetVisible(_lookDownButton, showLook);
                if (_panelObject != null)
                {
                    _panelObject.SetActive(showLook);
                }
                if (_panelRect != null)
                {
                    _panelRect.sizeDelta = new Vector2(260f, 28f + CountVisible(false, false, false, false, showLook, showLook) * 54f);
                }
                if (_statusText != null)
                {
                    _statusText.gameObject.SetActive(false);
                }
                LayoutVisibleButtons();
                return;
            }

            var leadInventory = driver.Model.GetInventory(driver.Model.LeadPlayerId);
            var secondInventory = driver.Model.GetInventory(driver.Model.SecondPlayerId);
            var localInventory = driver.Model.GetInventory(binder.LocalPlayerId);
            var showPlace = binder.CanPlaceLeadRivet;
            var showCollect = binder.TryGetNearestCollectableRivet(out _);
            var showSwitch = binder.CanSwitchLead;
            var showRescue = binder.CanRescuePull;
            var showAny = showPlace || showCollect || showSwitch || showRescue || showLook;
            var visibleCount = CountVisible(showPlace, showCollect, showSwitch, showRescue, showLook, showLook);
            var showStatusNow = showStatus && showAny;

            if (_panelObject != null)
            {
                _panelObject.SetActive(showAny);
            }

            if (_panelRect != null)
            {
                _panelRect.sizeDelta = new Vector2(260f, 28f + visibleCount * 54f + (showStatusNow ? 58f : 0f));
            }

            SetVisible(_placeButton, showPlace);
            SetVisible(_collectButton, showCollect);
            SetVisible(_switchButton, showSwitch);
            SetVisible(_rescueButton, showRescue);
            SetVisible(_lookUpButton, showLook);
            SetVisible(_lookDownButton, showLook);
            var statusY = LayoutVisibleButtons();

            if (_statusText != null)
            {
                _statusText.gameObject.SetActive(showAny && showStatusNow);
                _statusText.rectTransform.anchoredPosition = new Vector2(0f, statusY - 2f);
                _statusText.text =
                    $"自己 {localInventory} / 领攀 {leadInventory}\n" +
                    $"队友 {secondInventory} / 场上 {driver.Model.PlacedRivets.Count}\n" +
                    $"总计 {driver.Model.TotalInventoryAndPlacedCount()} / 收绳 {driver.Model.RescueState.PullAmount:0.0}m";
            }
        }

        private void OnPlaceClicked()
        {
            binder?.PlaceLeadRivetFromUi();
        }

        private void OnCollectClicked()
        {
            binder?.CollectNearestRivetFromUi();
        }

        private void OnSwitchClicked()
        {
            binder?.SwitchLeadFromUi(out _);
        }

        private void OnRescueClicked()
        {
            binder?.RescuePullFromUi();
            driver?.DebugResolveLeadFall();
        }

        private void BeginLook(int poseIndex)
        {
            if (_heldLookPoseIndex < 0)
            {
                _restoreLookPoseIndex = GetCurrentCameraPoseIndex();
            }

            _heldLookPoseIndex = poseIndex;
            SwitchCameraPose(poseIndex);
        }

        private void EndLook()
        {
            if (_heldLookPoseIndex < 0)
            {
                return;
            }

            var restoreIndex = _restoreLookPoseIndex;
            _heldLookPoseIndex = -1;
            _restoreLookPoseIndex = -1;

            if (restoreIndex >= 0)
            {
                SwitchCameraPose(restoreIndex);
            }
        }

        private void ApplyHeldLookPose()
        {
            if (_heldLookPoseIndex >= 0)
            {
                SwitchCameraPose(_heldLookPoseIndex);
            }
        }

        private float LayoutVisibleButtons()
        {
            var y = -20f;
            y = LayoutButton(_placeButton, y);
            y = LayoutButton(_collectButton, y);
            y = LayoutButton(_switchButton, y);
            y = LayoutButton(_rescueButton, y);
            y = LayoutButton(_lookUpButton, y);
            return LayoutButton(_lookDownButton, y);
        }

        private static float LayoutButton(Button button, float y)
        {
            if (button == null || !button.gameObject.activeSelf)
            {
                return y;
            }

            var rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(0f, y);
            }

            return y - 54f;
        }

        private static int CountVisible(bool place, bool collect, bool switchLead, bool rescue, bool lookUp, bool lookDown)
        {
            var count = 0;
            if (place)
            {
                count++;
            }

            if (collect)
            {
                count++;
            }

            if (switchLead)
            {
                count++;
            }

            if (rescue)
            {
                count++;
            }

            if (lookUp)
            {
                count++;
            }

            if (lookDown)
            {
                count++;
            }

            return count;
        }

        private Component ResolveCameraMgr()
        {
            if (_cameraMgr != null)
            {
                return _cameraMgr;
            }

            var cameraMgrType = ResolveType(CameraMgrTypeName);
            if (cameraMgrType == null)
            {
                return null;
            }

            _cameraMgr = FindObjectOfType(cameraMgrType) as Component;
            return _cameraMgr;
        }

        private bool SwitchCameraPose(int poseIndex)
        {
            var cameraMgr = ResolveCameraMgr();
            if (cameraMgr == null)
            {
                return false;
            }

            var method = cameraMgr.GetType().GetMethod("SwitchToFixedPose", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int) }, null);
            if (method == null)
            {
                return false;
            }

            method.Invoke(cameraMgr, new object[] { poseIndex });
            return true;
        }

        private int GetCurrentCameraPoseIndex()
        {
            var cameraMgr = ResolveCameraMgr();
            if (cameraMgr == null)
            {
                return -1;
            }

            var property = cameraMgr.GetType().GetProperty("CurrentIndex", BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
            {
                return -1;
            }

            var value = property.GetValue(cameraMgr, null);
            return value is int index ? index : -1;
        }

        private static Type ResolveType(string typeName)
        {
            var type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static void SetVisible(Button button, bool visible)
        {
            if (button != null)
            {
                button.gameObject.SetActive(visible);
                button.interactable = visible;
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            var eventSystem = new GameObject("Rivet Rope EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }
}
