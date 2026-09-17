using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Wapawapa.Abilities;

namespace Wapawapa.UI
{
    /// <summary>
    /// Keeps a normal screen-space canvas on desktop and routes it through the
    /// local stereo camera while an XR display is running.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class XrScreenSpaceCanvas : MonoBehaviour
    {
        [SerializeField, Min(0.25f)] private float vrPlaneDistance = 1.25f;

        private static readonly List<XRDisplaySubsystem> Displays = new();
        private static Camera cachedLocalCamera;

        private Canvas canvas;
        private RenderMode desktopRenderMode;
        private Camera desktopCamera;
        private float desktopPlaneDistance;
        private Camera xrCamera;
        private bool usingXr;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            desktopRenderMode = canvas.renderMode;
            desktopCamera = canvas.worldCamera;
            desktopPlaneDistance = canvas.planeDistance;
            Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (canvas == null)
            {
                return;
            }

            if (!IsXrDisplayRunning())
            {
                RestoreDesktopCanvas();
                return;
            }

            var nextCamera = ResolveLocalCamera();
            if (nextCamera == null)
            {
                return;
            }

            if (!usingXr || xrCamera != nextCamera || canvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                xrCamera = nextCamera;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = xrCamera;
                canvas.planeDistance = Mathf.Max(vrPlaneDistance, xrCamera.nearClipPlane + 0.05f);
                usingXr = true;
            }
        }

        private void RestoreDesktopCanvas()
        {
            if (!usingXr)
            {
                return;
            }

            canvas.renderMode = desktopRenderMode;
            canvas.worldCamera = desktopCamera;
            canvas.planeDistance = desktopPlaneDistance;
            xrCamera = null;
            usingXr = false;
        }

        private static bool IsXrDisplayRunning()
        {
            Displays.Clear();
            SubsystemManager.GetSubsystems(Displays);
            foreach (var display in Displays)
            {
                if (display != null && display.running)
                {
                    return true;
                }
            }

            return false;
        }

        private static Camera ResolveLocalCamera()
        {
            if (IsUsable(cachedLocalCamera))
            {
                return cachedLocalCamera;
            }

            foreach (var receiver in FindObjectsByType<PlayerDamageReceiver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (receiver == null || !receiver.IsLocalPlayer)
                {
                    continue;
                }

                var playerCamera = receiver.GetComponentInChildren<Camera>(true);
                if (IsUsable(playerCamera))
                {
                    cachedLocalCamera = playerCamera;
                    return cachedLocalCamera;
                }
            }

            var mainCamera = Camera.main;
            if (IsUsable(mainCamera))
            {
                cachedLocalCamera = mainCamera;
                return cachedLocalCamera;
            }

            foreach (var activeCamera in Camera.allCameras)
            {
                if (IsUsable(activeCamera))
                {
                    cachedLocalCamera = activeCamera;
                    return cachedLocalCamera;
                }
            }

            return null;
        }

        private static bool IsUsable(Camera candidate)
        {
            return candidate != null && candidate.enabled && candidate.gameObject.activeInHierarchy;
        }
    }
}
