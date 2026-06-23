using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wapawapa.Gameplay;
using Wapawapa.Networking;

namespace Wapawapa.Editor
{
    public static class WapawapaMultiplayerSetup
    {
        private const string Root = "Assets/_Project";
        private const string TitleScenePath = Root + "/Scenes/Title.unity";
        private const string GameScenePath = Root + "/Scenes/Game.unity";
        private const string PlayerPrefabPath = Root + "/Prefabs/NetworkPlayer.prefab";

        [MenuItem("Tools/Wapawapa/Generate Multiplayer Setup")]
        public static void Generate()
        {
            EnsureFolders();
            var playerPrefab = CreatePlayerPrefab();
            CreateTitleScene(playerPrefab);
            CreateGameScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(GameScenePath, true),
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            Debug.Log("Wapawapa multiplayer setup generated successfully.");
        }

        private static void EnsureFolders()
        {
            CreateFolder("Assets", "_Project");
            CreateFolder(Root, "Scenes");
            CreateFolder(Root, "Prefabs");
            CreateFolder(Root, "Materials");
            CreateFolder(Root, "Scripts");
            CreateFolder(Root + "/Scripts", "Core");
            CreateFolder(Root + "/Scripts", "Gameplay");
            CreateFolder(Root + "/Scripts", "Network");
            CreateFolder(Root + "/Scripts", "VR");
            CreateFolder(Root + "/Scripts", "Editor");
        }

        private static void CreateFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static GameObject CreatePlayerPrefab()
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = "NetworkPlayer";
            Object.DestroyImmediate(root.GetComponent<CapsuleCollider>());

            var characterController = root.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.3f;
            characterController.center = new Vector3(0f, 0.9f, 0f);

            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            var playerController = root.AddComponent<DesktopVrNetworkPlayer>();

            var head = CreateTrackedPart("Head", PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.65f, 0f), 0.28f);
            var leftHand = CreateTrackedPart("LeftHand", PrimitiveType.Sphere, root.transform, new Vector3(-0.32f, 1.25f, 0.38f), 0.14f);
            var rightHand = CreateTrackedPart("RightHand", PrimitiveType.Sphere, root.transform, new Vector3(0.32f, 1.25f, 0.38f), 0.14f);

            var cameraObject = new GameObject("PlayerCamera");
            cameraObject.transform.SetParent(head.transform, false);
            var playerCamera = cameraObject.AddComponent<Camera>();
            playerCamera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();

            var serializedController = new SerializedObject(playerController);
            serializedController.FindProperty("localCamera").objectReferenceValue = playerCamera;
            serializedController.FindProperty("head").objectReferenceValue = head.transform;
            serializedController.FindProperty("leftHand").objectReferenceValue = leftHand.transform;
            serializedController.FindProperty("rightHand").objectReferenceValue = rightHand.transform;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            return prefab;
        }

        private static GameObject CreateTrackedPart(string name, PrimitiveType type, Transform parent, Vector3 localPosition, float scale)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = Vector3.one * scale;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.AddComponent<NetworkTransform>();
            return part;
        }

        private static void CreateTitleScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Title";

            var cameraObject = new GameObject("Title Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.045f, 0.08f);

            var connectionObject = new GameObject("Room Connection Controller");
            var connection = connectionObject.AddComponent<RoomConnectionController>();

            var runnerObject = new GameObject("Fusion Runner");
            runnerObject.transform.SetParent(connectionObject.transform, false);
            var runner = runnerObject.AddComponent<NetworkRunner>();
            var sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();

            var serializedConnection = new SerializedObject(connection);
            serializedConnection.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
            serializedConnection.FindProperty("gameSceneBuildIndex").intValue = 1;
            serializedConnection.FindProperty("runner").objectReferenceValue = runner;
            serializedConnection.FindProperty("sceneManager").objectReferenceValue = sceneManager;
            serializedConnection.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, TitleScenePath);
        }

        private static void CreateGameScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Game";

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(3f, 1f, 3f);

            CreateLandmark("Blue Landmark", new Vector3(-4f, 1f, 4f), new Color(0.2f, 0.55f, 1f));
            CreateLandmark("Orange Landmark", new Vector3(4f, 1f, 4f), new Color(1f, 0.4f, 0.18f));
            CreateLandmark("Center Landmark", new Vector3(0f, 0.75f, 7f), new Color(0.35f, 0.9f, 0.5f));

            EditorSceneManager.SaveScene(scene, GameScenePath);
        }

        private static void CreateLandmark(string name, Vector3 position, Color color)
        {
            var landmark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            landmark.name = name;
            landmark.transform.position = position;
            landmark.transform.localScale = new Vector3(1.5f, 2f, 1.5f);
            landmark.GetComponent<Renderer>().sharedMaterial = CreateOrLoadMaterial(name.Replace(" ", string.Empty), color);
        }

        private static Material CreateOrLoadMaterial(string name, Color color)
        {
            var path = $"{Root}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
