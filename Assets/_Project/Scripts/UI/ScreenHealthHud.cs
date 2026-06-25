using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wapawapa.Abilities;

namespace Wapawapa.UI
{
    public sealed class ScreenHealthHud : MonoBehaviour
    {
        private static ScreenHealthHud instance;

        private readonly Dictionary<PlayerDamageReceiver, HudBar> barsByReceiver = new();

        private Canvas canvas;
        private HudBar localBar;
        private HudBar opponentBar;

        public static ScreenHealthHud Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            var hudObject = new GameObject("Screen Health HUD");
            instance = hudObject.AddComponent<ScreenHealthHud>();
            DontDestroyOnLoad(hudObject);
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            CreateUI();
        }

        private void Update()
        {
            BindReceivers();
            localBar.UpdateFill();
            opponentBar.UpdateFill();
        }

        private void BindReceivers()
        {
            var receivers = FindObjectsByType<PlayerDamageReceiver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            PlayerDamageReceiver local = null;
            PlayerDamageReceiver opponent = null;

            foreach (var receiver in receivers)
            {
                if (receiver == null)
                {
                    continue;
                }

                if (receiver.IsLocalPlayer)
                {
                    local = receiver;
                }
                else if (opponent == null)
                {
                    opponent = receiver;
                }
            }

            localBar.Bind(local);
            opponentBar.Bind(opponent);
        }

        private void CreateUI()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            localBar = CreateBar("Local HP", new Vector2(24f, -24f), new Vector2(0f, 1f), TextAlignmentOptions.Left, "YOU");
            opponentBar = CreateBar("Opponent HP", new Vector2(-24f, -24f), new Vector2(1f, 1f), TextAlignmentOptions.Right, "OPPONENT");
        }

        private HudBar CreateBar(string name, Vector2 anchoredPosition, Vector2 anchor, TextAlignmentOptions alignment, string label)
        {
            var root = new GameObject(name).AddComponent<RectTransform>();
            root.SetParent(canvas.transform, false);
            root.anchorMin = anchor;
            root.anchorMax = anchor;
            root.pivot = anchor;
            root.anchoredPosition = anchoredPosition;
            root.sizeDelta = new Vector2(420f, 54f);

            var labelText = CreateText("Label", root, label, 18, FontStyles.Bold, alignment);
            labelText.rectTransform.anchorMin = new Vector2(0f, 0.58f);
            labelText.rectTransform.anchorMax = new Vector2(1f, 1f);
            labelText.rectTransform.offsetMin = Vector2.zero;
            labelText.rectTransform.offsetMax = Vector2.zero;

            var background = CreateImage("Background", root, new Color(0.03f, 0.035f, 0.04f, 0.78f));
            background.rectTransform.anchorMin = new Vector2(0f, 0f);
            background.rectTransform.anchorMax = new Vector2(1f, 0.42f);
            background.rectTransform.offsetMin = Vector2.zero;
            background.rectTransform.offsetMax = Vector2.zero;

            var fill = CreateImage("Fill", background.rectTransform, new Color(0.1f, 0.9f, 0.35f, 0.95f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            var valueText = CreateText("Value", background.rectTransform, "100 / 100", 16, FontStyles.Bold, TextAlignmentOptions.Center);
            valueText.rectTransform.anchorMin = Vector2.zero;
            valueText.rectTransform.anchorMax = Vector2.one;
            valueText.rectTransform.offsetMin = Vector2.zero;
            valueText.rectTransform.offsetMax = Vector2.zero;

            var nameText = CreateText("Name", root, "", 18, FontStyles.Bold, alignment);
            nameText.color = new Color(1f, 0.95f, 0.55f, 1f);
            nameText.rectTransform.anchorMin = new Vector2(0f, -0.48f);
            nameText.rectTransform.anchorMax = new Vector2(1f, -0.04f);
            nameText.rectTransform.offsetMin = Vector2.zero;
            nameText.rectTransform.offsetMax = Vector2.zero;

            return new HudBar(root.gameObject, fill.rectTransform, valueText, nameText);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, int size, FontStyles style, TextAlignmentOptions alignment)
        {
            var label = new GameObject(name).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private sealed class HudBar
        {
            private readonly GameObject root;
            private readonly RectTransform fillRect;
            private readonly TextMeshProUGUI valueText;
            private readonly TextMeshProUGUI nameText;
            private PlayerDamageReceiver receiver;

            public HudBar(GameObject root, RectTransform fillRect, TextMeshProUGUI valueText, TextMeshProUGUI nameText)
            {
                this.root = root;
                this.fillRect = fillRect;
                this.valueText = valueText;
                this.nameText = nameText;
            }

            public void Bind(PlayerDamageReceiver nextReceiver)
            {
                receiver = nextReceiver;
                root.SetActive(receiver != null);
                UpdateFill();
            }

            public void UpdateFill()
            {
                if (receiver == null)
                {
                    return;
                }

                var maxHealth = Mathf.Max(1f, receiver.MaxHealth);
                var health = Mathf.Clamp(receiver.Health, 0f, maxHealth);
                var ratio = Mathf.Clamp01(health / maxHealth);
                fillRect.anchorMax = new Vector2(ratio, 1f);
                valueText.text = $"{health:0} / {maxHealth:0}";
                nameText.text = receiver.DisplayName;
            }
        }
    }
}
