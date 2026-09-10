using System;
using System.IO;
using System.Reflection;
using Fusion;
using UnityEditor;
using UnityEngine;
using Wapawapa.Gameplay;

namespace Wapawapa.Editor
{
    public static class TrackedAvatarValidation
    {
        private const string Request = "Temp/validate-tracked-avatar.request";
        private const string Result = "Temp/tracked-avatar-validation.txt";

        [InitializeOnLoadMethod]
        private static void CheckRequestedValidation()
        {
            if (File.Exists(Request)) EditorApplication.delayCall += RunRequested;
        }

        private static void RunRequested()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(Request);
            Run();
        }

        [MenuItem("Tools/Wapawapa/Validate Tracked Avatars")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Run avatar validation outside Play Mode.");
                return;
            }
            try
            {
                ValidatePrefab("Assets/_Project/Prefabs/Testing/AbilityTestPlayer.prefab", false);
                ValidatePrefab("Assets/_Project/Prefabs/NetworkPlayer.prefab", true);
                var property = typeof(DesktopVrNetworkPlayer).GetProperty("NetworkHandInput", BindingFlags.NonPublic | BindingFlags.Instance);
                Require(property != null && Attribute.IsDefined(property, typeof(NetworkedWeavedAttribute)), "Fusion must weave the grip/trigger network property.");
                File.WriteAllText(Result, "PASS: both player prefabs import without missing scripts, all avatar references resolve, Humanoid IK and finger input execute, and Fusion hand data is woven.\n");
                Debug.Log("Tracked avatar validation passed for both player prefabs.");
            }
            catch (Exception e)
            {
                File.WriteAllText(Result, e.ToString());
                Debug.LogException(e);
            }
        }

        private static void ValidatePrefab(string path, bool network)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0, path + ": missing script on " + node.name);
                Require(root.GetComponent<Wapawapa.Gestures.AirGestureRecorder>() != null, path + ": gesture recorder missing");
                Require(root.GetComponent<Wapawapa.Gestures.AirGestureRecognizer>() != null, path + ": gesture recognizer missing");
                Require(root.GetComponent<Wapawapa.GestureActions.GestureActionRouter>() != null, path + ": gesture router missing");
                Require(root.GetComponent<Wapawapa.GestureActions.AbilityLoadoutGestureAdapter>() != null, path + ": gesture adapter missing");
                Require(root.GetComponent<Wapawapa.Gestures.AirGestureDebugView>() != null, path + ": gesture debug view missing");
                var driver = root.GetComponent<TrackedAvatar>();
                Require(driver != null, path + ": avatar driver missing");
                var serialized = new SerializedObject(driver);
                foreach (string field in new[] { "avatarRoot", "headTarget", "leftHandTarget", "rightHandTarget", "ownerCamera" })
                    Require(serialized.FindProperty(field).objectReferenceValue != null, path + ": unset " + field);
                Require(driver.Initialize(), path + ": invalid Humanoid");
                driver.SetHandInput(new Vector4(1f, 1f, 0f, 0f));
                for (int i = 0; i < 20; i++) driver.Simulate(0.02f);
                if (network)
                {
                    Require(root.GetComponent<NetworkObject>() != null, "NetworkObject must remain attached");
                    Require(root.GetComponent<DesktopVrNetworkPlayer>() != null, "Network movement must remain attached");
                    var player = root.GetComponent<DesktopVrNetworkPlayer>();
                    typeof(DesktopVrNetworkPlayer).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(player, null);
                    var setVisible = typeof(DesktopVrNetworkPlayer).GetMethod("SetLocalAvatarVisible", BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (bool visible in new[] { false, true })
                    {
                        setVisible.Invoke(player, new object[] { visible });
                        Require(!root.GetComponent<Renderer>().enabled, "Primitive body must stay hidden with tracked avatar");
                        Require(!root.transform.Find("Head").GetComponent<Renderer>().enabled, "Primitive head must stay hidden with tracked avatar");
                    }
                    var cc = root.GetComponent<CharacterController>();
                    Require(cc != null && Mathf.Abs(cc.center.y - cc.height * 0.5f) < 0.01f, "Controller origin must be at feet");
                }
                foreach (string name in new[] { "LeftHand", "RightHand" })
                {
                    Transform hand = root.transform.Find(name);
                    Require(hand != null && hand.GetComponent<Wapawapa.Boxing.PunchHitbox>() != null, "Punch target retained");
                    Require(!hand.GetComponent<Renderer>().enabled, "Primitive hand mesh hidden");
                }
            }
            finally
            {
                // Test objects are discarded; the source prefab is never saved by this check.
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
