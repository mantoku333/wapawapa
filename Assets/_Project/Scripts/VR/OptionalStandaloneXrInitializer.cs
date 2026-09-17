using System;
using UnityEngine;
using UnityEngine.XR.Management;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wapawapa.VR
{
    public static class OptionalStandaloneXrInitializer
    {
#if UNITY_EDITOR
        private const string EditorXrPreferenceKey = "Wapawapa.EnableXrInEditorPlayMode";
        private const string EditorXrMenuPath = "Wapawapa/XR/Enable XR In Editor Play Mode";
#endif

#if UNITY_STANDALONE || UNITY_EDITOR
        private static XRManagerSettings manuallyStartedManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeWhenRequested()
        {
            var shouldEnableXr = false;
            foreach (var argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, "-enableXR", StringComparison.OrdinalIgnoreCase))
                {
                    shouldEnableXr = true;
                    break;
                }
            }

#if UNITY_EDITOR
            // Editor Play mode uses XR by default, so launching Unity through
            // PowerShell is no longer required. The menu toggle preserves an
            // easy desktop-only workflow for developers without a connected HMD.
            shouldEnableXr |= IsEditorXrEnabled();
#endif

            if (!shouldEnableXr)
            {
                Debug.Log("Wapawapa desktop mode active. Enable XR from the Wapawapa/XR menu or start with -enableXR.");
                return;
            }

            var settings = XRGeneralSettings.Instance;
            if (settings == null || settings.Manager == null)
            {
                Debug.LogError("XR Management settings are unavailable.");
                return;
            }

            // Automatically managed XR owns its own shutdown lifecycle.
            if (settings.InitManagerOnStart)
            {
                return;
            }

            manuallyStartedManager = settings.Manager;
            Application.quitting -= ShutdownManualXr;
            Application.quitting += ShutdownManualXr;
            if (settings.Manager.activeLoader == null)
            {
                settings.Manager.InitializeLoaderSync();
            }
            if (settings.Manager.activeLoader == null)
            {
                Debug.LogError("No Standalone XR loader could be initialized.");
                return;
            }

            settings.Manager.StartSubsystems();
            Debug.Log("Wapawapa Standalone XR initialized.");
        }

        private static void ShutdownManualXr()
        {
            Application.quitting -= ShutdownManualXr;
            var manager = manuallyStartedManager;
            manuallyStartedManager = null;
#if UNITY_EDITOR
            // Static fields may be reset by a Play Mode domain reload.
            var settings = XRGeneralSettings.Instance;
            if (manager == null && settings != null && !settings.InitManagerOnStart)
            {
                manager = settings.Manager;
            }
#endif
            if (manager == null || manager.activeLoader == null)
            {
                return;
            }

            // DeinitializeLoader also stops the subsystems before destroying them.
            manager.DeinitializeLoader();
            Debug.Log("Wapawapa Standalone XR stopped and deinitialized.");
        }
#endif

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterEditorShutdown()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= ShutdownManualXr;
            AssemblyReloadEvents.beforeAssemblyReload += ShutdownManualXr;
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                ShutdownManualXr();
            }
        }

        private static bool IsEditorXrEnabled()
        {
            return EditorPrefs.GetBool(EditorXrPreferenceKey, true);
        }

        [MenuItem(EditorXrMenuPath)]
        private static void ToggleEditorXr()
        {
            var enabled = !IsEditorXrEnabled();
            EditorPrefs.SetBool(EditorXrPreferenceKey, enabled);
            Menu.SetChecked(EditorXrMenuPath, enabled);
            Debug.Log($"Wapawapa XR in Editor Play mode: {(enabled ? "enabled" : "disabled")}");
        }

        [MenuItem(EditorXrMenuPath, true)]
        private static bool ValidateEditorXrToggle()
        {
            Menu.SetChecked(EditorXrMenuPath, IsEditorXrEnabled());
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
#endif
    }
}
