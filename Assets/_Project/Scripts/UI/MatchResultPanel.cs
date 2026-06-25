using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wapawapa.Abilities;
using Wapawapa.Networking;

namespace Wapawapa.UI
{
    public sealed class MatchResultPanel : MonoBehaviour
    {
        private static MatchResultPanel instance;

        private Canvas canvas;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI detailText;

        public static MatchResultPanel Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            var panelObject = new GameObject("Match Result Panel");
            instance = panelObject.AddComponent<MatchResultPanel>();
            DontDestroyOnLoad(panelObject);
            return instance;
        }

        public static void ShowFor(PlayerDamageReceiver knockedOut)
        {
            Ensure().Show(knockedOut);
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
            Hide();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Update()
        {
            if (canvas == null || !canvas.enabled)
            {
                return;
            }

            var receivers = FindObjectsByType<PlayerDamageReceiver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (receivers.Length == 0)
            {
                return;
            }

            foreach (var receiver in receivers)
            {
                if (receiver.IsKnockedOut)
                {
                    return;
                }
            }

            Hide();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Hide();
        }

        private void Show(PlayerDamageReceiver knockedOut)
        {
            if (knockedOut == null)
            {
                return;
            }

            var localLost = knockedOut.IsLocalPlayer;
            titleText.text = localLost ? "YOU LOSE" : "YOU WIN";
            detailText.text = localLost ? "Your HP reached zero." : "Opponent HP reached zero.";
            canvas.enabled = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Hide()
        {
            if (canvas != null)
            {
                canvas.enabled = false;
            }
        }

        private void RestartRound()
        {
            foreach (var receiver in FindObjectsByType<PlayerDamageReceiver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                receiver.RequestResetHealth();
                receiver.RequestRespawnAtSpawnPoint();
            }

            Hide();
        }

        private void LeaveRoom()
        {
            var controller = FindFirstObjectByType<RoomConnectionController>();
            if (controller != null)
            {
                controller.RequestLeaveRoom();
                return;
            }

            var runner = FindFirstObjectByType<NetworkRunner>();
            if (runner != null)
            {
                _ = runner.Shutdown();
            }

            SceneManager.LoadScene(0);
        }

        private void CreateUI()
        {
            EnsureEventSystem();

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            gameObject.AddComponent<GraphicRaycaster>();

            var dim = CreateImage("Dim", transform, new Color(0f, 0f, 0f, 0.62f));
            Stretch(dim.rectTransform, Vector2.zero, Vector2.one);

            var panel = CreateImage("Panel", transform, new Color(0.08f, 0.09f, 0.1f, 0.96f));
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(520f, 300f);
            panelRect.anchoredPosition = Vector2.zero;

            titleText = CreateText("Title", panel.transform, "RESULT", 46, FontStyles.Bold, TextAlignmentOptions.Center);
            var titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.64f);
            titleRect.anchorMax = new Vector2(1f, 0.92f);
            titleRect.offsetMin = new Vector2(28f, 0f);
            titleRect.offsetMax = new Vector2(-28f, 0f);

            detailText = CreateText("Detail", panel.transform, "", 20, FontStyles.Normal, TextAlignmentOptions.Center);
            var detailRect = detailText.rectTransform;
            detailRect.anchorMin = new Vector2(0f, 0.46f);
            detailRect.anchorMax = new Vector2(1f, 0.62f);
            detailRect.offsetMin = new Vector2(28f, 0f);
            detailRect.offsetMax = new Vector2(-28f, 0f);

            CreateButton("RestartButton", panel.transform, "RESTART", new Vector2(-120f, -88f), RestartRound);
            CreateButton("LeaveButton", panel.transform, "LEAVE", new Vector2(120f, -88f), LeaveRoom);
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
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Max(10f, size * 0.55f);
            label.fontSizeMax = size;
            return label;
        }

        private static void CreateButton(string name, Transform parent, string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action)
        {
            var buttonImage = CreateImage(name, parent, new Color(0.92f, 0.93f, 0.95f, 1f));
            var rect = buttonImage.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(180f, 54f);
            rect.anchoredPosition = anchoredPosition;

            var button = buttonImage.gameObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(action);

            var text = CreateText("Label", buttonImage.transform, label, 20, FontStyles.Bold, TextAlignmentOptions.Center);
            text.color = new Color(0.06f, 0.07f, 0.08f, 1f);
            Stretch(text.rectTransform, Vector2.zero, Vector2.one);
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
                if (inputModule == null)
                {
                    inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                if (inputModule.actionsAsset == null)
                {
                    inputModule.AssignDefaultActions();
                }

                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            DontDestroyOnLoad(eventSystemObject);
            eventSystemObject.AddComponent<EventSystem>();
            var createdInputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            createdInputModule.AssignDefaultActions();
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
