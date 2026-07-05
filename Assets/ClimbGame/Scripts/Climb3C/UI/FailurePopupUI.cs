using UnityEngine;
using UnityEngine.UI;

namespace ClimbGame.Climb3C.UI
{
    public sealed class FailurePopupUI : MonoBehaviour
    {
        private GameObject _root;
        private Text _message;

        public void Build(Canvas canvas)
        {
            _root = new GameObject("FailurePopup");
            _root.transform.SetParent(canvas.transform, false);
            var rect = _root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var backdrop = _root.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.62f);

            var panel = new GameObject("Panel", typeof(RectTransform));
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.SetParent(_root.transform, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(520f, 220f);

            var panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.08f, 0.08f, 0.94f);

            var titleGo = new GameObject("Title", typeof(RectTransform));
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.SetParent(panel.transform, false);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -34f);
            titleRect.sizeDelta = new Vector2(-40f, 58f);
            var title = titleGo.AddComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.text = "攀登失败";
            title.fontSize = 34;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(1f, 0.32f, 0.24f);

            var messageGo = new GameObject("Message", typeof(RectTransform));
            var messageRect = messageGo.GetComponent<RectTransform>();
            messageRect.SetParent(panel.transform, false);
            messageRect.anchorMin = new Vector2(0f, 0f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.offsetMin = new Vector2(40f, 34f);
            messageRect.offsetMax = new Vector2(-40f, -98f);
            _message = messageGo.AddComponent<Text>();
            _message.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _message.fontSize = 22;
            _message.alignment = TextAnchor.MiddleCenter;
            _message.color = Color.white;

            Hide();
        }

        public void Show(string reason)
        {
            if (_message != null)
            {
                _message.text = string.IsNullOrEmpty(reason)
                    ? "生命值归零，无法继续攀爬。"
                    : "生命值归零，无法继续攀爬。\n" + reason;
            }

            if (_root != null)
            {
                _root.SetActive(true);
            }
        }

        public void Hide()
        {
            if (_root != null)
            {
                _root.SetActive(false);
            }
        }
    }
}
