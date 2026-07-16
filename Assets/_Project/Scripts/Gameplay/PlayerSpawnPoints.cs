using Fusion;
using UnityEngine;

namespace Wapawapa.Gameplay
{
    public static class PlayerSpawnPoints
    {
        public const string Player1Name = "StartPoint1";
        public const string Player2Name = "StartPoint2";
        private const string LegacyPlayer1Name = "Player1SpawnPoint";
        private const string LegacyPlayer2Name = "Player2SpawnPoint";

        public static int GetSlot(PlayerRef player)
        {
            if (player == PlayerRef.None)
            {
                return 0;
            }

            var playerId = player.PlayerId > 0 ? player.PlayerId : player.AsIndex;
            return Mathf.Abs(playerId - 1) % 2;
        }

        public static Pose GetSpawnPose(PlayerRef player)
        {
            return GetSpawnPose(GetSlot(player));
        }

        public static Pose GetSpawnPose(int slot)
        {
            var fallbackPosition = slot == 0 ? new Vector3(-1.5f, 0f, 0f) : new Vector3(1.5f, 0f, 0f);
            var spawnName = slot == 0 ? Player1Name : Player2Name;
            var legacySpawnName = slot == 0 ? LegacyPlayer1Name : LegacyPlayer2Name;
            var spawnObject = GameObject.Find(spawnName);
            if (spawnObject == null)
            {
                spawnObject = GameObject.Find(legacySpawnName);
            }

            if (spawnObject == null)
            {
                Debug.LogWarning($"Spawn point '{spawnName}' was not found. Falling back to {fallbackPosition}.");
                return new Pose(fallbackPosition, Quaternion.identity);
            }

            return new Pose(spawnObject.transform.position, spawnObject.transform.rotation);
        }
    }
}
