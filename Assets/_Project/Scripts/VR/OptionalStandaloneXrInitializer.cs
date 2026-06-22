using System;
using UnityEngine;
using UnityEngine.XR.Management;

namespace Wapawapa.VR
{
    public static class OptionalStandaloneXrInitializer
    {
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

            if (!shouldEnableXr)
            {
                Debug.Log("Wapawapa desktop mode active. Start with -enableXR for Standalone PCVR.");
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
    }
}
