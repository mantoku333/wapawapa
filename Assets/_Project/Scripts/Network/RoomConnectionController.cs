using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wapawapa.Networking
{
    public sealed class RoomConnectionController : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private int gameSceneBuildIndex = 1;

        private NetworkRunner runner;
        private string roomKey = string.Empty;
        private string status = "Enter a room key to create or join a room.";
        private bool isConnecting;
        private bool localPlayerJoined;

        public NetworkRunner Runner => runner;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
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
                _ = ConnectAsync();
                break;
            }
        }

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().buildIndex == 0)
            {
                DrawTitleScreen();
            }
            else
            {
                DrawGameStatus();
            }
        }

        private void DrawTitleScreen()
        {
            const float width = 520f;
            const float height = 310f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUILayout.BeginArea(rect, GUI.skin.window);
            GUILayout.Space(18f);
            GUILayout.Label("WAPAWAPA", TitleStyle());
            GUILayout.Space(12f);
            GUILayout.Label("Photon Fusion VR Multiplayer", CenteredLabelStyle(18));
            GUILayout.Space(24f);
            GUILayout.Label("ROOM KEY", CenteredLabelStyle(14));
            GUI.enabled = !isConnecting;
            roomKey = GUILayout.TextField(roomKey, 32, GUILayout.Height(42f));
            GUILayout.Space(12f);

            if (GUILayout.Button(isConnecting ? "CONNECTING..." : "CREATE / JOIN", GUILayout.Height(48f)))
            {
                _ = ConnectAsync();
            }

            GUI.enabled = true;
            GUILayout.Space(12f);
            GUILayout.Label(status, CenteredLabelStyle(13));
            GUILayout.EndArea();
        }

        private void DrawGameStatus()
        {
            var playerCount = 0;
            if (runner != null && runner.IsRunning)
            {
                foreach (var _ in runner.ActivePlayers)
                {
                    playerCount++;
                }
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 360f, 150f), GUI.skin.box);
            GUILayout.Label($"Players: {playerCount} / 2");
            GUILayout.Label("Desktop: WASD + Mouse | Esc unlocks cursor");
            GUILayout.Label("VR: Head/controllers + left stick movement");
            if (GUILayout.Button("LEAVE ROOM"))
            {
                _ = LeaveRoomAsync();
            }
            GUILayout.EndArea();
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

            var runnerObject = new GameObject("Fusion Runner");
            runnerObject.transform.SetParent(transform);
            runner = runnerObject.AddComponent<NetworkRunner>();
            runner.ProvideInput = false;
            runner.AddCallbacks(this);

            var sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
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
                Destroy(runnerObject);
                runner = null;
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

            var slot = Math.Abs(runner.LocalPlayer.AsIndex) % 2;
            var spawnPosition = slot == 0 ? new Vector3(-1.5f, 0f, 0f) : new Vector3(1.5f, 0f, 0f);
            var playerObject = runner.Spawn(playerPrefab.GetComponent<NetworkObject>(), spawnPosition, Quaternion.identity, runner.LocalPlayer);
            runner.SetPlayerObject(runner.LocalPlayer, playerObject);
            Debug.Log($"Wapawapa local player spawned. PlayerId={runner.LocalPlayer.PlayerId}");
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

        private static GUIStyle TitleStyle()
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 36,
                fontStyle = FontStyle.Bold,
            };
        }

        private static GUIStyle CenteredLabelStyle(int size)
        {
            return new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = size,
                wordWrap = true,
            };
        }
    }
}
