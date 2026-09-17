using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wapawapa.Abilities;
using Wapawapa.Gameplay;
using Wapawapa.UI;

namespace Wapawapa.Networking
{
    public sealed class RoomConnectionController : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private int gameSceneBuildIndex = 1;
        [SerializeField] private NetworkRunner runner;
        [SerializeField] private NetworkSceneManagerDefault sceneManager;

        private string roomKey = string.Empty;
        private string playerName = string.Empty;
        private string status = "Enter a room key to create or join a room.";
        private bool isConnecting;
        private bool localPlayerJoined;
        private Canvas uiCanvas;
        private GameObject titleUi;
        private GameObject gameStatusUi;
        private TMP_InputField playerNameInput;
        private TMP_InputField roomKeyInput;
        private Button connectButton;
        private TextMeshProUGUI connectButtonText;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI playerCountText;

        public NetworkRunner Runner => runner;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            CreateUi();
        }

        private void Start()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length - 1; i++)
            {
                if (!string.Equals(arguments[i], "-roomKey", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                roomKey = arguments[i + 1];
                roomKeyInput?.SetTextWithoutNotify(roomKey);
                _ = ConnectAsync();
                break;
            }
        }

        private void Update()
        {
            RefreshUi();
        }

        private void RefreshUi()
        {
            if (uiCanvas == null)
            {
                return;
            }

            var onTitleScreen = SceneManager.GetActiveScene().buildIndex == 0;
            titleUi.SetActive(onTitleScreen);
            gameStatusUi.SetActive(!onTitleScreen);

            connectButton.interactable = !isConnecting;
            roomKeyInput.interactable = !isConnecting;
            playerNameInput.interactable = !isConnecting;
            connectButtonText.text = isConnecting ? "CONNECTING..." : "CREATE / JOIN";
            statusText.text = status;

            if (!playerNameInput.isFocused && playerNameInput.text != playerName)
            {
                playerNameInput.SetTextWithoutNotify(playerName);
            }

            if (!roomKeyInput.isFocused && roomKeyInput.text != roomKey)
            {
                roomKeyInput.SetTextWithoutNotify(roomKey);
            }

            var playerCount = 0;
            if (runner != null && runner.IsRunning)
            {
                foreach (var _ in runner.ActivePlayers)
                {
                    playerCount++;
                }
            }

            playerCountText.text = $"Players: {playerCount} / 2";
        }

        public void RequestLeaveRoom()
        {
            _ = LeaveRoomAsync();
        }

        private async Task ConnectAsync()
        {
            var trimmedKey = roomKey.Trim();
            if (trimmedKey.Length < 3)
            {
                status = "Room key must contain at least 3 characters.";
                return;
            }

            if (playerPrefab == null)
            {
                status = "Player prefab is not configured.";
                return;
            }

            isConnecting = true;
            status = "Connecting to Photon Cloud...";

            if (runner == null)
            {
                status = "NetworkRunner is not configured in the scene.";
                isConnecting = false;
                return;
            }

            if (sceneManager == null)
            {
                status = "NetworkSceneManagerDefault is not configured in the scene.";
                isConnecting = false;
                return;
            }

            runner.ProvideInput = false;
            runner.AddCallbacks(this);

            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(SceneRef.FromIndex(gameSceneBuildIndex), LoadSceneMode.Single);

            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Shared,
                SessionName = BuildPrivateSessionName(trimmedKey),
                PlayerCount = 2,
                IsVisible = false,
                IsOpen = true,
                Scene = sceneInfo,
                SceneManager = sceneManager,
            });

            if (!result.Ok)
            {
                status = $"Connection failed: {result.ShutdownReason}";
                isConnecting = false;
            }
        }

        private async Task LeaveRoomAsync()
        {
            if (runner != null)
            {
                await runner.Shutdown();
            }

            Destroy(gameObject);
            SceneManager.LoadScene(0);
        }

        private static string BuildPrivateSessionName(string key)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes($"wapawapa:{key}"));
            var builder = new StringBuilder("ww-");
            for (var i = 0; i < 12; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }

            return builder.ToString();
        }

        private void TrySpawnLocalPlayer()
        {
            if (!localPlayerJoined || runner == null || !runner.IsRunning)
            {
                return;
            }

            if (SceneManager.GetActiveScene().buildIndex != gameSceneBuildIndex)
            {
                return;
            }

            if (runner.TryGetPlayerObject(runner.LocalPlayer, out _))
            {
                return;
            }

            var spawnPose = PlayerSpawnPoints.GetSpawnPose(runner.LocalPlayer);
            var playerObject = runner.Spawn(playerPrefab.GetComponent<NetworkObject>(), spawnPose.position, spawnPose.rotation, runner.LocalPlayer);
            var damageReceiver = playerObject.GetComponent<PlayerDamageReceiver>();
            if (damageReceiver != null)
            {
                damageReceiver.SetPlayerName(GetDisplayPlayerName());
            }

            runner.SetPlayerObject(runner.LocalPlayer, playerObject);
            Debug.Log($"Wapawapa local player spawned. PlayerId={runner.LocalPlayer.PlayerId}");
        }

        private string GetDisplayPlayerName()
        {
            var trimmedName = playerName.Trim();
            return string.IsNullOrWhiteSpace(trimmedName)
                ? $"Player {runner.LocalPlayer.PlayerId}"
                : trimmedName;
        }

        public void OnPlayerJoined(NetworkRunner networkRunner, PlayerRef player)
        {
            Debug.Log($"Wapawapa player joined. PlayerId={player.PlayerId}");
            if (player == networkRunner.LocalPlayer)
            {
                localPlayerJoined = true;
                TrySpawnLocalPlayer();
            }
        }

        public void OnSceneLoadDone(NetworkRunner networkRunner)
        {
            TrySpawnLocalPlayer();
        }

        public void OnShutdown(NetworkRunner networkRunner, ShutdownReason shutdownReason)
        {
            status = $"Disconnected: {shutdownReason}";
            isConnecting = false;
        }

        public void OnPlayerLeft(NetworkRunner networkRunner, PlayerRef player) { }
        public void OnInput(NetworkRunner networkRunner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner networkRunner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner networkRunner) { }
        public void OnDisconnectedFromServer(NetworkRunner networkRunner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner networkRunner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner networkRunner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner networkRunner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner networkRunner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner networkRunner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner networkRunner, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadStart(NetworkRunner networkRunner) { }
        public void OnObjectExitAOI(NetworkRunner networkRunner, NetworkObject networkObject, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner networkRunner, NetworkObject networkObject, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner networkRunner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner networkRunner, PlayerRef player, ReliableKey key, float progress) { }

        private void CreateUi()
        {
            EnsureEventSystem();

            var canvasObject = new GameObject("Room Connection UI", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            uiCanvas = canvasObject.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiCanvas.sortingOrder = 400;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            canvasObject.AddComponent<XrScreenSpaceCanvas>();

            titleUi = CreateRect("Title UI", canvasObject.transform).gameObject;
            Stretch((RectTransform)titleUi.transform);
            var titleDim = CreateImage("Dim", titleUi.transform, new Color(0.015f, 0.025f, 0.05f, 0.76f));
            Stretch(titleDim.rectTransform);

            var titlePanel = CreateImage("Panel", titleUi.transform, new Color(0.055f, 0.075f, 0.11f, 0.97f));
            SetCenteredRect(titlePanel.rectTransform, new Vector2(620f, 510f), Vector2.zero);

            var title = CreateText("Title", titlePanel.transform, "WAPAWAPA", 48, FontStyles.Bold, TextAlignmentOptions.Center);
            SetCenteredRect(title.rectTransform, new Vector2(560f, 64f), new Vector2(0f, 198f));

            var subtitle = CreateText("Subtitle", titlePanel.transform, "Photon Fusion VR Multiplayer", 20, FontStyles.Normal, TextAlignmentOptions.Center);
            SetCenteredRect(subtitle.rectTransform, new Vector2(560f, 36f), new Vector2(0f, 150f));

            var playerLabel = CreateText("Player Name Label", titlePanel.transform, "PLAYER NAME", 15, FontStyles.Bold, TextAlignmentOptions.Center);
            SetCenteredRect(playerLabel.rectTransform, new Vector2(520f, 28f), new Vector2(0f, 101f));
            playerNameInput = CreateInputField("Player Name", titlePanel.transform, "Player name", new Vector2(520f, 48f), new Vector2(0f, 61f), 20);
            playerNameInput.onValueChanged.AddListener(value => playerName = value);

            var roomLabel = CreateText("Room Key Label", titlePanel.transform, "ROOM KEY", 15, FontStyles.Bold, TextAlignmentOptions.Center);
            SetCenteredRect(roomLabel.rectTransform, new Vector2(520f, 28f), new Vector2(0f, 9f));
            roomKeyInput = CreateInputField("Room Key", titlePanel.transform, "At least 3 characters", new Vector2(520f, 52f), new Vector2(0f, -34f), 32);
            roomKeyInput.onValueChanged.AddListener(value => roomKey = value);

            connectButton = CreateButton("Connect", titlePanel.transform, "CREATE / JOIN", new Vector2(520f, 58f), new Vector2(0f, -112f), () => _ = ConnectAsync(), out connectButtonText);

            statusText = CreateText("Status", titlePanel.transform, status, 16, FontStyles.Normal, TextAlignmentOptions.Center);
            statusText.textWrappingMode = TextWrappingModes.Normal;
            SetCenteredRect(statusText.rectTransform, new Vector2(540f, 62f), new Vector2(0f, -181f));

            gameStatusUi = CreateRect("Game Status UI", canvasObject.transform).gameObject;
            Stretch((RectTransform)gameStatusUi.transform);
            var statusPanel = CreateImage("Panel", gameStatusUi.transform, new Color(0.035f, 0.045f, 0.06f, 0.88f));
            var statusPanelRect = statusPanel.rectTransform;
            statusPanelRect.anchorMin = Vector2.zero;
            statusPanelRect.anchorMax = Vector2.zero;
            statusPanelRect.pivot = Vector2.zero;
            statusPanelRect.anchoredPosition = new Vector2(24f, 24f);
            statusPanelRect.sizeDelta = new Vector2(470f, 208f);

            playerCountText = CreateText("Player Count", statusPanel.transform, "Players: 0 / 2", 20, FontStyles.Bold, TextAlignmentOptions.Left);
            SetTopLeftRect(playerCountText.rectTransform, new Vector2(420f, 30f), new Vector2(24f, -18f));

            var instructions = CreateText(
                "Instructions",
                statusPanel.transform,
                "Desktop: WASD + Mouse | Esc unlocks cursor\nVR: Head/controllers + left stick movement\nAbilities: 1 Shockwave | 2 Railway | 3 Penguin",
                15,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft);
            instructions.textWrappingMode = TextWrappingModes.Normal;
            SetTopLeftRect(instructions.rectTransform, new Vector2(422f, 82f), new Vector2(24f, -54f));

            CreateButton("Leave", statusPanel.transform, "LEAVE ROOM", new Vector2(190f, 42f), new Vector2(0f, -76f), RequestLeaveRoom, out _);
            RefreshUi();
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            string value,
            float fontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static TMP_InputField CreateInputField(
            string name,
            Transform parent,
            string placeholderValue,
            Vector2 size,
            Vector2 position,
            int characterLimit)
        {
            var background = CreateImage(name, parent, new Color(0.92f, 0.94f, 0.98f, 1f));
            SetCenteredRect(background.rectTransform, size, position);

            var input = background.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.characterLimit = characterLimit;
            input.lineType = TMP_InputField.LineType.SingleLine;

            var viewport = CreateRect("Viewport", background.transform);
            Stretch(viewport, new Vector2(16f, 5f), new Vector2(-16f, -5f));
            viewport.gameObject.AddComponent<RectMask2D>();

            var valueText = CreateText("Text", viewport, string.Empty, 20f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            valueText.color = new Color(0.035f, 0.045f, 0.06f, 1f);
            Stretch(valueText.rectTransform);

            var placeholder = CreateText("Placeholder", viewport, placeholderValue, 20f, FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
            placeholder.color = new Color(0.2f, 0.23f, 0.28f, 0.58f);
            Stretch(placeholder.rectTransform);

            input.textViewport = viewport;
            input.textComponent = valueText;
            input.placeholder = placeholder;
            return input;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 size,
            Vector2 position,
            UnityEngine.Events.UnityAction action,
            out TextMeshProUGUI labelText)
        {
            var image = CreateImage(name, parent, new Color(0.92f, 0.94f, 0.98f, 1f));
            SetCenteredRect(image.rectTransform, size, position);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            labelText = CreateText("Label", image.transform, label, 19f, FontStyles.Bold, TextAlignmentOptions.Center);
            labelText.color = new Color(0.035f, 0.045f, 0.06f, 1f);
            Stretch(labelText.rectTransform);
            return button;
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem != null)
            {
                var legacyModule = eventSystem.GetComponent<StandaloneInputModule>();
                if (legacyModule != null)
                {
                    legacyModule.enabled = false;
                }

                var existingModule = eventSystem.GetComponent<InputSystemUIInputModule>();
                if (existingModule == null)
                {
                    existingModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                if (existingModule.actionsAsset == null)
                {
                    existingModule.AssignDefaultActions();
                }

                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            DontDestroyOnLoad(eventSystemObject);
            eventSystemObject.AddComponent<EventSystem>();
            var inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        private static void SetCenteredRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void SetTopLeftRect(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect)
        {
            Stretch(rect, Vector2.zero, Vector2.zero);
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
