using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wapawapa.Gameplay
{
    public sealed class GameFlowAudio : MonoBehaviour
    {
        private const string GameSceneName = "Game";
        private static GameFlowAudio instance;

        private AudioSource startVoiceSource;
        private AudioSource startMusicSource;
        private AudioSource backgroundMusicSource;
        private AudioSource resultSource;
        private Coroutine startRoutine;
        private bool resultPlayed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        public static void PlayGameStart()
        {
            instance?.BeginGameStartPlayback();
        }

        public static void PlayResult()
        {
            instance?.BeginResultPlayback();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameSceneName || instance != null)
            {
                return;
            }

            var audioObject = new GameObject(nameof(GameFlowAudio));
            SceneManager.MoveGameObjectToScene(audioObject, scene);
            var controller = audioObject.AddComponent<GameFlowAudio>();
            controller.Initialize();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Initialize()
        {
            instance = this;
            var settings = Resources.Load<GameFlowAudioSettings>(nameof(GameFlowAudioSettings));
            if (settings == null)
            {
                Debug.LogWarning("[GameFlowAudio] GameFlowAudioSettings could not be loaded.", this);
                return;
            }

            startVoiceSource = CreateSource("Game Start Voice", settings.GameStartVoice, 1f);
            startMusicSource = CreateSource("Game Start Music", settings.GameStartMusic, 0.75f);
            backgroundMusicSource = CreateSource("Background Music", settings.BackgroundMusic, 0.55f, true);
            resultSource = CreateSource("Result Sting", settings.ResultSting, 1f);
            BeginGameStartPlayback();
        }

        private AudioSource CreateSource(string sourceName, AudioClip clip, float volume, bool loop = false)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            var source = sourceObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }

        private void BeginGameStartPlayback()
        {
            resultPlayed = false;
            resultSource?.Stop();
            startVoiceSource?.Stop();
            startMusicSource?.Stop();
            backgroundMusicSource?.Stop();

            if (startRoutine != null)
            {
                StopCoroutine(startRoutine);
            }
            startRoutine = StartCoroutine(PlayStartWhenListenerIsReady());
        }

        private IEnumerator PlayStartWhenListenerIsReady()
        {
            while (!HasActiveAudioListener())
            {
                yield return null;
            }

            startVoiceSource?.Play();
            startMusicSource?.Play();

            while (IsPlaying(startVoiceSource) || IsPlaying(startMusicSource))
            {
                yield return null;
            }

            backgroundMusicSource?.Play();
            startRoutine = null;
        }

        private void BeginResultPlayback()
        {
            if (resultPlayed)
            {
                return;
            }

            resultPlayed = true;
            if (startRoutine != null)
            {
                StopCoroutine(startRoutine);
                startRoutine = null;
            }
            startVoiceSource?.Stop();
            startMusicSource?.Stop();
            backgroundMusicSource?.Stop();
            resultSource?.Play();
        }

        private static bool IsPlaying(AudioSource source)
        {
            return source != null && source.clip != null && source.isPlaying;
        }

        private static bool HasActiveAudioListener()
        {
            foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (listener.enabled && listener.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
