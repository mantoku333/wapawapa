using Fusion;
using UnityEngine;

namespace Wapawapa.Gameplay
{
    public static class PlayerSpawnPoints
    {
        public const string Player1Name = "Player1SpawnPoint";
        public const string Player2Name = "Player2SpawnPoint";

        public static int GetSlot(PlayerRef player)
        {
            return Mathf.Abs(player.AsIndex) % 2;
        }

        public static Pose GetSpawnPose(PlayerRef player)
        {
            return GetSpawnPose(GetSlot(player));
        }

        public static Pose GetSpawnPose(int slot)
        {
            var fallbackPosition = slot == 0 ? new Vector3(-1.5f, 0f, 0f) : new Vector3(1.5f, 0f, 0f);
            var spawnName = slot == 0 ? Player1Name : Player2Name;
            var spawnObject = GameObject.Find(spawnName);
            if (spawnObject == null)
            {
                return new Pose(fallbackPosition, Quaternion.identity);
            }

            return new Pose(spawnObject.transform.position, spawnObject.transform.rotation);
        }
    }
}
