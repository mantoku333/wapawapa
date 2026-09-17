using System;
using UnityEditor;
using UnityEngine;

namespace Wapawapa.EditorTools
{
    public static class RailwayVehiclePrefabGenerator
    {
        private const string OutputFolder = "Assets/_Project/Prefabs/Abilities";
        private const string CurrentTrainPrefabPath = OutputFolder + "/KoryoinDeportationTrain.prefab";
        private const string HelicopterModelPath = "Assets/Helicopter/Resources/Models/HelicopterModel/OH-58D.fbx";
        private const string SubwayModelPath = "Assets/SubwayTrain/SubwayTrain.fbx";

        [MenuItem("Wapawapa/Abilities/Generate Railway Vehicle Prefabs")]
        public static void GenerateRailwayVehiclePrefabs()
        {
            EnsureFolder(OutputFolder);

            var currentTrain = LoadRequired<GameObject>(CurrentTrainPrefabPath);
            var helicopter = LoadRequired<GameObject>(HelicopterModelPath);
            var subway = LoadRequired<GameObject>(SubwayModelPath);

            SaveSinglePrefab(
                "RailwayHelicopter",
                helicopter,
                new Vector3(0f, 0f, 0f),
                Vector3.zero,
                Vector3.one * 3.5f);

            SaveSinglePrefab(
                "SubwayTrainCar",
                subway,
                Vector3.zero,
                Vector3.zero,
                Vector3.one);

            SaveLinkedPrefab(
                "KoryoinDeportationTrainSevenCars",
                currentTrain,
                7,
                7.5f,
                Vector3.zero,
                Vector3.zero,
                Vector3.one);

            SaveLinkedPrefab(
                "SubwayTrainSevenCars",
                subway,
                7,
                7.5f,
                Vector3.zero,
                Vector3.zero,
                Vector3.one);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated railway vehicle prefabs.");
        }

        private static void SaveSinglePrefab(
            string prefabName,
            GameObject source,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            SaveLinkedPrefab(prefabName, source, 1, 0f, localPosition, localEulerAngles, localScale);
        }

        private static void SaveLinkedPrefab(
            string prefabName,
            GameObject source,
            int count,
            float spacing,
            Vector3 localPosition,
            Vector3 localEulerAngles,
            Vector3 localScale)
        {
            var root = new GameObject(prefabName);
            try
            {
                var firstOffset = (count - 1) * spacing * -0.5f;
                for (var i = 0; i < count; i++)
                {
                    var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
                    if (instance == null)
                    {
                        throw new InvalidOperationException($"Failed to instantiate {source.name}.");
                    }

                    instance.name = count > 1 ? $"{source.name} Car {i + 1:00}" : source.name;
                    instance.transform.SetParent(root.transform, false);
                    instance.transform.localPosition = localPosition + Vector3.forward * (firstOffset + spacing * i);
                    instance.transform.localRotation = Quaternion.Euler(localEulerAngles);
                    instance.transform.localScale = localScale;
                    RemoveColliders(instance);
                }

                PrefabUtility.SaveAsPrefabAsset(root, $"{OutputFolder}/{prefabName}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static T LoadRequired<T>(string path)
            where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"Could not load asset at {path}.");
            }

            return asset;
        }

        private static void RemoveColliders(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
