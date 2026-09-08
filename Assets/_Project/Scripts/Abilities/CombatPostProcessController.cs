using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
