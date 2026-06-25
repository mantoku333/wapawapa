using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wapawapa.EditorTools
{
    public static class XinZhaoScenePlacement
    {
        private const string ModelPath = "Assets/_Project/Models/XinZhao/xin_zhao.glb";
        private const string ScenePath = "Assets/_Project/Scenes/Test/AbilityTest_Fuyuno.unity";
        private const string ObjectName = "Xin Zhao";
        private const float TargetHeight = 2f;

        [MenuItem("Wapawapa/Scene/Place Xin Zhao In Fuyuno")]
        public static void PlaceXinZhaoInFuyuno()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);

            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelPrefab == null)
            {
                throw new InvalidOperationException(
                    $"Could not import {ModelPath} as a GameObject. Add a GLB/glTF importer package, then run this menu again.");
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RemoveExistingObject(scene);

            var instance = PrefabUtility.InstantiatePrefab(modelPrefab, scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"Failed to instantiate {ModelPath}.");
            }

            instance.name = ObjectName;
            instance.transform.SetPositionAndRotation(new Vector3(0f, 0f, 4f), Quaternion.Euler(0f, 180f, 0f));
            instance.transform.localScale = Vector3.one;

            FitToTargetHeight(instance);
            MoveBottomToGround(instance);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Placed {ObjectName} in {ScenePath}.");
        }

        private static void RemoveExistingObject(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == ObjectName)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    return;
                }
            }
        }

        private static void FitToTargetHeight(GameObject instance)
        {
            if (!TryGetRendererBounds(instance, out var bounds) || bounds.size.y <= 0f)
            {
                return;
            }

            var scale = TargetHeight / bounds.size.y;
            instance.transform.localScale = Vector3.one * scale;
        }

        private static void MoveBottomToGround(GameObject instance)
        {
            if (!TryGetRendererBounds(instance, out var bounds))
            {
                return;
            }

            instance.transform.position += Vector3.up * -bounds.min.y;
        }

        private static bool TryGetRendererBounds(GameObject instance, out Bounds bounds)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }
    }
}
