using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using Wapawapa.Gameplay;

namespace Wapawapa.Editor
{
    public static class AvatarLocomotionValidation
    {
        [MenuItem("Tools/Wapawapa/Validate Locomotion Animation")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run validation outside Play Mode.");
            string modelPath = AssetDatabase.FindAssets("t:Model")
                .Select(AssetDatabase.GUIDToAssetPath).First(path => Path.GetFileName(path) == "まこら.fbx");
            var root = new GameObject("Locomotion validation");
            root.transform.position = new Vector3(10000f, 0f, 10000f);
            var floor = new GameObject("Validation floor");
            floor.transform.position = root.transform.position + Vector3.down * 0.5f;
            floor.AddComponent<BoxCollider>().size = new Vector3(100f, 1f, 100f);
            try
            {
                var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
                var head = Target(root.transform, "Head", new Vector3(0f, 1.65f, 0f));
                var left = Target(root.transform, "Left", new Vector3(-0.32f, 1.25f, 0.38f));
                var right = Target(root.transform, "Right", new Vector3(0.32f, 1.25f, 0.38f));
                var driver = root.AddComponent<TrackedAvatar>();
                Set(driver, "avatarRoot", model.transform);
                Set(driver, "headTarget", head);
                Set(driver, "leftHandTarget", left);
                Set(driver, "rightHandTarget", right);
                Set(driver, "locomotionController", AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AvatarLocomotionAssets.ControllerPath));
                Require(driver.Initialize(), "Humanoid initialization failed");
                Physics.SyncTransforms();
                var animator = model.GetComponent<Animator>();
                var locomotion = Get<AvatarLocomotionAnimation>(driver, "locomotion");
                var playable = Get<AnimatorControllerPlayable>(locomotion, "controller");
                var thigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                Tick(driver, root.transform, Vector3.zero, 30);
                Require(Dominant(playable) == "Idle", "Rest must select Idle");

                foreach (var sample in new[] {
                    (Vector3.forward, "Running"), (Vector3.back, "Running Backward"),
                    (Vector3.left, "Left Strafe"), (Vector3.right, "Right Strafe") })
                {
                    Tick(driver, root.transform, sample.Item1 * 3.5f, 60);
                    Require(Dominant(playable) == sample.Item2, "Wrong direction clip: " + sample.Item2);
                    Quaternion initial = thigh.localRotation;
                    float largest = 0f;
                    for (int frame = 0; frame < 30; frame++)
                    {
                        Tick(driver, root.transform, sample.Item1 * 3.5f, 1);
                        largest = Mathf.Max(largest, Quaternion.Angle(initial, thigh.localRotation));
                    }
                    Require(largest > 5f, sample.Item2 + " must actually animate the retargeted leg");
                }
                Tick(driver, root.transform, Vector3.zero, 90);
                Require(Dominant(playable) == "Idle", "Stopping must return to Idle");
                Vector3 rootBefore = root.transform.position;
                Tick(driver, root.transform, Vector3.zero, 30);
                Require(Vector3.Distance(rootBefore, root.transform.position) < 0.0001f, "Animation must not move the gameplay root");

                Tick(driver, root.transform, Vector3.up * 3f, 15);
                Require(playable.GetCurrentAnimatorStateInfo(0).IsName("Jump"), "Ascent must select Jump");
                Tick(driver, root.transform, Vector3.down, 15);
                Require(playable.GetCurrentAnimatorStateInfo(0).IsName("Fall"), "Descent must select Fall");
                root.transform.position = new Vector3(root.transform.position.x, 0f, root.transform.position.z);
                Tick(driver, root.transform, Vector3.zero, 30);
                Require(playable.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"), "Landing must return to locomotion");

                head.rotation = Quaternion.Euler(15f, 25f, 5f);
                driver.SetHandInput(new Vector4(1f, 1f, 0f, 0f));
                Tick(driver, root.transform, Vector3.zero, 30);
                var skull = animator.GetBoneTransform(HumanBodyBones.Head);
                Vector3 eye = skull.TransformPoint(Get<Vector3>(driver, "eyeInHead"));
                Require(Vector3.Distance(eye, head.position) < 0.001f, "Animated body must keep eyes aligned to tracking");
                var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Require(Vector3.Distance(hand.position, left.position + left.rotation * new Vector3(0f, 0f, -0.04f)) < 0.03f,
                    "Tracked hand must override the animation");
                var finger = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
                Quaternion closed = finger.localRotation;
                driver.SetHandInput(Vector4.zero);
                Tick(driver, root.transform, Vector3.zero, 30);
                Require(Quaternion.Angle(closed, finger.localRotation) > 20f, "Finger input must override animation");
                driver.SetLocalView(true);
                Vector3 headBefore = skull.position;
                Vector3 footBefore = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                var beforeRender = typeof(TrackedAvatar).GetMethod("BeforeRender", BindingFlags.Instance | BindingFlags.NonPublic);
                for (int i = 0; i < 10; i++) beforeRender.Invoke(driver, null);
                Require(Vector3.Distance(headBefore, skull.position) < 0.0001f, "BeforeRender must not accumulate head offsets");
                Require(Vector3.Distance(footBefore, animator.GetBoneTransform(HumanBodyBones.LeftFoot).position) < 0.0001f,
                    "BeforeRender must not advance the animation");
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/locomotion-validation.txt", "PASS: all seven Humanoid clips, directional blending, moving legs, stop, jump/fall/landing, root motion disabled, head/hand/finger tracking, and repeated BeforeRender.\n");
                Debug.Log("Locomotion validation passed.");
            }
            finally
            {
                root.GetComponent<TrackedAvatar>()?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(floor);
            }
        }

        private static Transform Target(Transform root, string name, Vector3 position)
        {
            var target = new GameObject(name).transform;
            target.SetParent(root, false);
            target.localPosition = position;
            return target;
        }
        private static void Tick(TrackedAvatar driver, Transform root, Vector3 velocity, int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                root.position += velocity * 0.02f;
                driver.Simulate(0.02f);
            }
        }
        private static string Dominant(AnimatorControllerPlayable playable) => playable.GetCurrentAnimatorClipInfo(0)
            .OrderByDescending(info => info.weight).First().clip.name;
        private static void Set(object target, string field, object value) => target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
