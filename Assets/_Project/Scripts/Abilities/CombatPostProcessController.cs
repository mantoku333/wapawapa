using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Wapawapa.Rendering;

namespace Wapawapa.Abilities
{
    /// <summary>
    /// ローカル画面だけに適用する、一時的な戦闘ポストプロセス演出を管理します。
    /// </summary>
    public sealed class CombatPostProcessController : MonoBehaviour
    {
        private static CombatPostProcessController instance;

        private Volume volume;
        private ColorAdjustments colorAdjustments;
        private Coroutine activeEffect;
        private GameObject hitDebugMarker;
        private Canvas debugCanvas;
        private Texture2D debugCircleTexture;
        private bool hasLoggedCreation;

        public static CombatPostProcessController Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject(nameof(CombatPostProcessController));
                    instance = go.AddComponent<CombatPostProcessController>();
                }

                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            CreateVolume();
        }

        public void PlayBloodFocusStrikeFlash(
            float duration,
            float fadeIn,
            float fadeOut,
            float saturation,
            float contrast,
            int flashCount,
            float interval)
        {
            EnsureVolume();
            EnsureCameraPostProcessing();
            Debug.Log($"[CombatPostProcessController] Flash requested: duration={duration}, saturation={saturation}, contrast={contrast}, count={flashCount}", this);
            if (activeEffect != null)
            {
                StopCoroutine(activeEffect);
            }

            activeEffect = StartCoroutine(PlayBloodFocusStrikeFlashRoutine(
                duration,
                fadeIn,
                fadeOut,
                saturation,
                contrast,
                flashCount,
                interval));
        }

        public void ShowHitPositionDebug(Vector3 worldPosition)
        {
            var camera = Camera.main;
            if (camera == null && Camera.allCamerasCount > 0) camera = Camera.allCameras[0];
            if (camera == null) return;

            var screen = camera.WorldToScreenPoint(worldPosition);
            if (screen.z <= 0f) return;

            EnsureDebugCanvas();
            if (hitDebugMarker == null)
            {
                hitDebugMarker = new GameObject("Blood Focus Hit Position Debug");
                hitDebugMarker.transform.SetParent(debugCanvas.transform, false);
                var image = hitDebugMarker.AddComponent<Image>();
                image.sprite = CreateDebugCircleSprite();
                image.color = Color.red;
                image.raycastTarget = false;
                var rect = image.rectTransform;
                rect.sizeDelta = new Vector2(64f, 64f);
            }

            var markerRect = hitDebugMarker.GetComponent<RectTransform>();
            markerRect.position = screen;
            hitDebugMarker.SetActive(true);
            CancelInvoke(nameof(HideHitPositionDebug));
            Invoke(nameof(HideHitPositionDebug), 1.5f);
        }

        private void EnsureDebugCanvas()
        {
            if (debugCanvas != null) return;
            var canvasObject = new GameObject("Blood Focus Debug Overlay");
            canvasObject.transform.SetParent(transform, false);
            debugCanvas = canvasObject.AddComponent<Canvas>();
            debugCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            debugCanvas.sortingOrder = 5000;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        private Sprite CreateDebugCircleSprite()
        {
            if (debugCircleTexture == null)
            {
                const int size = 64;
                debugCircleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                var center = (size - 1) * 0.5f;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                        pixels[y * size + x] = distance <= 29f && distance >= 24f ? Color.red : Color.clear;
                    }
                }
                debugCircleTexture.SetPixels(pixels);
                debugCircleTexture.Apply();
            }

            return Sprite.Create(debugCircleTexture, new Rect(0, 0, debugCircleTexture.width, debugCircleTexture.height), new Vector2(0.5f, 0.5f), 64f);
        }

        private void HideHitPositionDebug()
        {
            if (hitDebugMarker != null) hitDebugMarker.SetActive(false);
        }

        public void PlayRadialGrayscale(Vector3 worldPosition, float duration, float startRadius, float endRadius, float edge)
        {
            EnsureRadialGrayscaleFeature();
            var camera = Camera.main;
            if (camera == null && Camera.allCamerasCount > 0) camera = Camera.allCameras[0];
            if (camera == null) return;

            var viewport = camera.WorldToViewportPoint(worldPosition);
            if (viewport.z <= 0f) return;

            BloodFocusRadialGrayscaleFeature.Center = viewport;
            BloodFocusRadialGrayscaleFeature.Radius = startRadius;
            BloodFocusRadialGrayscaleFeature.Edge = edge;
            BloodFocusRadialGrayscaleFeature.Strength = 1f;
            Debug.Log($"[CombatPostProcessController] Radial grayscale requested center={viewport}, radius={startRadius}->{endRadius}", this);
            StartCoroutine(AnimateRadialGrayscale(duration, startRadius, endRadius));
        }

        private IEnumerator AnimateRadialGrayscale(float duration, float startRadius, float endRadius)
        {
            var elapsed = 0f;
            while (elapsed < Mathf.Max(0.01f, duration))
            {
                elapsed += Time.unscaledDeltaTime;
                BloodFocusRadialGrayscaleFeature.Radius = Mathf.Lerp(startRadius, endRadius, elapsed / duration);
                yield return null;
            }
            BloodFocusRadialGrayscaleFeature.Strength = 0f;
        }

        private void EnsureRadialGrayscaleFeature()
        {
            var rendererData = Resources.FindObjectsOfTypeAll<ScriptableRendererData>();
            for (var i = 0; i < rendererData.Length; i++)
            {
                if (rendererData[i] == null) continue;
                var exists = false;
                for (var j = 0; j < rendererData[i].rendererFeatures.Count; j++)
                {
                    if (rendererData[i].rendererFeatures[j] is BloodFocusRadialGrayscaleFeature) { exists = true; break; }
                }
                if (!exists)
                {
                    var feature = ScriptableObject.CreateInstance<BloodFocusRadialGrayscaleFeature>();
                    rendererData[i].rendererFeatures.Add(feature);
                    feature.Create();
                    rendererData[i].SetDirty();
                }
            }
        }


        private IEnumerator PlayBloodFocusStrikeFlashRoutine(
            float duration,
            float fadeIn,
            float fadeOut,
            float saturation,
            float contrast,
            int flashCount,
            float interval)
        {
            EnsureVolume();
            colorAdjustments.saturation.value = Mathf.Clamp(saturation, -100f, 0f);
            colorAdjustments.contrast.value = Mathf.Clamp(contrast, -100f, 100f);
            colorAdjustments.active = true;
            volume.enabled = true;

            var count = Mathf.Max(1, flashCount);
            for (var i = 0; i < count; i++)
            {
                yield return FadeWeight(0f, 1f, fadeIn);
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, duration));
                yield return FadeWeight(1f, 0f, fadeOut);

                if (i < count - 1)
                {
                    yield return new WaitForSecondsRealtime(Mathf.Max(0f, interval));
                }
            }

            activeEffect = null;
        }

        private IEnumerator FadeWeight(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                volume.weight = to;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                volume.weight = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }

            volume.weight = to;
            Debug.Log($"[CombatPostProcessController] Volume weight reached {to}.", this);
        }

        private void EnsureVolume()
        {
            if (volume == null || colorAdjustments == null)
            {
                CreateVolume();
            }
        }

        private void EnsureCameraPostProcessing()
        {
            var targetCamera = Camera.main;
            if (targetCamera == null)
            {
                var cameras = Camera.allCameras;
                for (var i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i] != null && cameras[i].isActiveAndEnabled)
                    {
                        targetCamera = cameras[i];
                        break;
                    }
                }

                if (targetCamera == null)
                {
                    Debug.LogWarning("[CombatPostProcessController] No active camera was found.", this);
                    return;
                }

                Debug.Log($"[CombatPostProcessController] Camera.main was not found; using active camera '{targetCamera.name}'.", targetCamera);
            }

            var cameraData = targetCamera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = targetCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                Debug.Log($"[CombatPostProcessController] Added UniversalAdditionalCameraData to camera '{targetCamera.name}'.", targetCamera);
            }

            cameraData.volumeLayerMask |= 1 << volume.gameObject.layer;

            if (!cameraData.renderPostProcessing)
            {
                cameraData.renderPostProcessing = true;
                Debug.Log($"[CombatPostProcessController] Enabled URP post processing on camera '{targetCamera.name}'.", targetCamera);
            }
            else
            {
                Debug.Log($"[CombatPostProcessController] Camera '{targetCamera.name}' already has URP post processing enabled.", targetCamera);
            }
        }

        private void CreateVolume()
        {
            volume = gameObject.GetComponent<Volume>();
            if (volume == null)
            {
                volume = gameObject.AddComponent<Volume>();
            }

            volume.isGlobal = true;
            volume.enabled = true;
            volume.priority = 1000f;
            volume.weight = 0f;
            volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            colorAdjustments = volume.profile.Add<ColorAdjustments>(true);
            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = -100f;
            colorAdjustments.contrast.overrideState = true;
            colorAdjustments.contrast.value = 100f;
            if (!hasLoggedCreation)
            {
                Debug.Log("[CombatPostProcessController] Runtime global Volume created.", this);
                hasLoggedCreation = true;
            }
        }
    }
}
