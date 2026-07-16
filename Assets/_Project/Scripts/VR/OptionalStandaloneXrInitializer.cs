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

            settings.Manager.InitializeLoaderSync();
            if (settings.Manager.activeLoader == null)
            {
                Debug.LogError("No Standalone XR loader could be initialized.");
                return;
            }

            settings.Manager.StartSubsystems();
            Debug.Log("Wapawapa Standalone XR initialized.");
        }
#endif

#if UNITY_EDITOR
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
