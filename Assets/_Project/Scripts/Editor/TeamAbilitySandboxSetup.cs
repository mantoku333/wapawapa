using System.IO;
using System.Linq;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Wapawapa.Abilities;
using Wapawapa.Boxing;
using Wapawapa.Testing;

namespace Wapawapa.Editor
{
    public static class TeamAbilitySandboxSetup
    {
        private const string Root = "Assets/_Project";
        private const string PlayerPrefabPath = Root + "/Prefabs/NetworkPlayer.prefab";
        private const string TestPlayerPrefabPath = Root + "/Prefabs/Testing/AbilityTestPlayer.prefab";
        private const string BagPrefabPath = Root + "/Prefabs/Testing/BoxingSandbag.prefab";
        private const string ShockwaveEffectPrefabPath = Root + "/Prefabs/Abilities/ShockwaveEffect.prefab";
        private const string TestSceneFolder = Root + "/Scenes/Test";

        private static readonly string[] MemberSceneNames =
        {
            "AbilityTest_Member01",
            "AbilityTest_Member02",
            "AbilityTest_Member03",
            "AbilityTest_Member04",
            "AbilityTest_Member05",
            "AbilityTest_Member06",
        };

        [MenuItem("Tools/Wapawapa/Generate Team Ability Sandbox")]
        public static void Generate()
        {
            EnsureFolders();
            var shockwaveEffect = CreateShockwaveEffectPrefab();
            var bagPrefab = CreateBagPrefab();
            var testPlayerPrefab = CreateLocalTestPlayerPrefab(shockwaveEffect);
            UpgradeNetworkPlayerPrefab(shockwaveEffect);
            CreateMemberScenes(testPlayerPrefab, bagPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            Debug.Log("Wapawapa team ability sandbox generated successfully.");
        }

        [MenuItem("Tools/Wapawapa/Repair Ability Test Scenes")]
        public static void RepairAbilityTestScenes()
        {
            AssetDatabase.Refresh();

            var scenePaths = AssetDatabase
                .FindAssets("t:Scene", new[] { TestSceneFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith("AbilityTest_"))
                .OrderBy(path => path)
                .ToArray();

            foreach (var scenePath in scenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                {
                    RepairPrefabInstance(root);
                    RemoveMissingScriptsRecursive(root);
                }

                EditorSceneManager.SaveScene(scene);
                Debug.Log($"Repaired ability test scene: {scenePath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets", "_Project");
            CreateFolder(Root, "Prefabs");
            CreateFolder(Root + "/Prefabs", "Abilities");
            CreateFolder(Root + "/Prefabs", "Testing");
            CreateFolder(Root, "Scenes");
            CreateFolder(Root + "/Scenes", "Test");
        }

        private static void CreateFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void RepairPrefabInstance(GameObject root)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(root))
            {
                return;
            }

            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(root);
            if (instanceRoot == root)
            {
                PrefabUtility.RevertPrefabInstance(instanceRoot, InteractionMode.AutomatedAction);
            }
        }

        private static void RemoveMissingScriptsRecursive(GameObject root)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            foreach (Transform child in root.transform)
            {
                RemoveMissingScriptsRecursive(child.gameObject);
            }
        }

        private static GameObject CreateShockwaveEffectPrefab()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            root.name = "ShockwaveEffect";
            root.transform.localScale = new Vector3(1.25f, 0.04f, 1.25f);
            Object.DestroyImmediate(root.GetComponent<Collider>());

            var renderer = root.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateOrLoadMaterial("ShockwaveEffect", new Color(0.2f, 0.75f, 1f, 0.65f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ShockwaveEffectPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBagPrefab()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "BoxingSandbag";
            root.transform.localScale = new Vector3(0.65f, 1.35f, 0.65f);

            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 3f;
            rigidbody.linearDamping = 1.25f;
            rigidbody.angularDamping = 1.25f;

            var target = root.AddComponent<BoxingTarget>();
            var serializedTarget = new SerializedObject(target);
            serializedTarget.FindProperty("targetRigidbody").objectReferenceValue = rigidbody;
            serializedTarget.ApplyModifiedPropertiesWithoutUndo();

            root.GetComponent<Renderer>().sharedMaterial = CreateOrLoadMaterial("Sandbag", new Color(0.95f, 0.75f, 0.35f));

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BagPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateLocalTestPlayerPrefab(GameObject shockwaveEffect)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "AbilityTestPlayer";
            root.transform.position = new Vector3(0f, 0.9f, -3f);
            Object.DestroyImmediate(root.GetComponent<CapsuleCollider>());
            root.AddComponent<PlayerDamageReceiver>();
            var punchSettings = root.AddComponent<PlayerPunchSettings>();

            var head = CreatePart("Head", PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.65f, 0f), 0.28f);
            var leftHand = CreatePart("LeftHand", PrimitiveType.Sphere, root.transform, new Vector3(-0.35f, 1.25f, 0.45f), 0.16f);
            var rightHand = CreatePart("RightHand", PrimitiveType.Sphere, root.transform, new Vector3(0.35f, 1.25f, 0.45f), 0.16f);

            EnsurePunchHand(leftHand.gameObject, punchSettings);
            EnsurePunchHand(rightHand.gameObject, punchSettings);

            var cameraObject = new GameObject("TestCamera");
            cameraObject.transform.SetParent(head, false);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();

            var rig = root.AddComponent<KeyboardHandTestRig>();
            ConfigureRig(rig, head, leftHand, rightHand);

            var ability = root.AddComponent<ForwardShockwaveAbility>();
            ConfigureShockwave(ability, shockwaveEffect, "sample.shockwave", "Sample Shockwave");

            var loadout = root.AddComponent<AbilityLoadout>();
            ConfigureLoadout(loadout, head, leftHand, rightHand, ability);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, TestPlayerPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Transform CreatePart(string name, PrimitiveType primitiveType, Transform parent, Vector3 localPosition, float scale)
        {
            var part = GameObject.CreatePrimitive(primitiveType);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = Vector3.one * scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            return part.transform;
        }

        private static void UpgradeNetworkPlayerPrefab(GameObject shockwaveEffect)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"NetworkPlayer prefab not found: {PlayerPrefabPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            var head = root.transform.Find("Head");
            var leftHand = root.transform.Find("LeftHand");
            var rightHand = root.transform.Find("RightHand");

            if (root.GetComponent<PlayerDamageReceiver>() == null)
            {
                root.AddComponent<PlayerDamageReceiver>();
            }

            var punchSettings = root.GetComponent<PlayerPunchSettings>();
            if (punchSettings == null)
            {
                punchSettings = root.AddComponent<PlayerPunchSettings>();
            }

            if (leftHand != null)
            {
                EnsurePunchHand(leftHand.gameObject, punchSettings);
            }

            if (rightHand != null)
            {
                EnsurePunchHand(rightHand.gameObject, punchSettings);
            }

            var ability = root.GetComponent<ForwardShockwaveAbility>();
            if (ability == null)
            {
                ability = root.AddComponent<ForwardShockwaveAbility>();
            }
            ConfigureShockwave(ability, shockwaveEffect, "sample.shockwave", "Sample Shockwave");

            var loadout = root.GetComponent<AbilityLoadout>();
            if (loadout == null)
            {
                loadout = root.AddComponent<AbilityLoadout>();
            }
            ConfigureLoadout(loadout, head, leftHand, rightHand, ability);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void EnsureSphereTrigger(GameObject target)
        {
            var collider = target.GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = target.AddComponent<SphereCollider>();
            }

            collider.isTrigger = true;
        }

        private static void EnsurePunchHand(GameObject target, PlayerPunchSettings punchSettings)
        {
            EnsureSphereTrigger(target);
            var hitbox = target.GetComponent<PunchHitbox>();
            if (hitbox == null)
            {
                hitbox = target.AddComponent<PunchHitbox>();
            }

            hitbox.SetPunchSettings(punchSettings);
        }

        private static void ConfigureRig(KeyboardHandTestRig rig, Transform head, Transform leftHand, Transform rightHand)
        {
            var serialized = new SerializedObject(rig);
            serialized.FindProperty("head").objectReferenceValue = head;
            serialized.FindProperty("leftHand").objectReferenceValue = leftHand;
            serialized.FindProperty("rightHand").objectReferenceValue = rightHand;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureShockwave(ForwardShockwaveAbility ability, GameObject effectPrefab, string abilityId, string abilityName)
        {
            var serialized = new SerializedObject(ability);
            serialized.FindProperty("abilityId").stringValue = abilityId;
            serialized.FindProperty("abilityName").stringValue = abilityName;
            serialized.FindProperty("effectPrefab").objectReferenceValue = effectPrefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureLoadout(AbilityLoadout loadout, Transform head, Transform leftHand, Transform rightHand, AbilityBase ability)
        {
            var serialized = new SerializedObject(loadout);
            serialized.FindProperty("head").objectReferenceValue = head;
            serialized.FindProperty("leftHand").objectReferenceValue = leftHand;
            serialized.FindProperty("rightHand").objectReferenceValue = rightHand;

            var slots = serialized.FindProperty("slots");
            slots.arraySize = 1;
            var slot = slots.GetArrayElementAtIndex(0);
            slot.FindPropertyRelative("label").stringValue = "Sample Shockwave";
            slot.FindPropertyRelative("activationKey").intValue = (int)Key.Digit1;
            slot.FindPropertyRelative("ability").objectReferenceValue = ability;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateMemberScenes(GameObject testPlayerPrefab, GameObject bagPrefab)
        {
            foreach (var sceneName in MemberSceneNames)
            {
                var scenePath = $"{TestSceneFolder}/{sceneName}.unity";
                if (File.Exists(scenePath))
                {
                    continue;
                }

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                scene.name = sceneName;

                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Floor";
                floor.transform.localScale = new Vector3(3f, 1f, 3f);

                PrefabUtility.InstantiatePrefab(testPlayerPrefab);

                for (var i = 0; i < 3; i++)
                {
                    var bag = (GameObject)PrefabUtility.InstantiatePrefab(bagPrefab);
                    bag.transform.position = new Vector3((i - 1) * 1.5f, 1.35f, 1.8f);
                }

                var note = new GameObject("Scene Note");
                note.AddComponent<TestSceneNote>();

                EditorSceneManager.SaveScene(scene, scenePath);
            }
        }

        private static Material CreateOrLoadMaterial(string name, Color color)
        {
            var materialPath = $"{Root}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, materialPath);
            return material;
        }
    }
}
