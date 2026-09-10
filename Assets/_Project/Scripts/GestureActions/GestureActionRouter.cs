using System;
using UnityEngine;
using Wapawapa.Gestures;

namespace Wapawapa.GestureActions
{
    public sealed class GestureActionRouter : MonoBehaviour
    {
        [SerializeField] private AirGestureRecognizer recognizer;
        [SerializeField] private GestureActionBinding[] bindings =
        {
            new GestureActionBinding("triangle", "ability.slot.1"),
            new GestureActionBinding("square", "ability.slot.0"),
            new GestureActionBinding("pe", "ability.slot.2"),
        };
        [SerializeField] private float actionCooldown = 0.2f;
        [SerializeField] private bool logActions;

        private float nextActionTime;

        public event Action<string, AirGestureResult> ActionRequested;

        public void ConfirmActivation(bool accepted)
        {
            if (recognizer != null) recognizer.ConfirmActivation(accepted);
        }

        private void OnEnable()
        {
            if (recognizer != null)
            {
                recognizer.GestureRecognized += HandleGestureRecognized;
            }
        }

        private void OnDisable()
        {
            if (recognizer != null)
            {
                recognizer.GestureRecognized -= HandleGestureRecognized;
            }
        }

        private void HandleGestureRecognized(AirGestureResult result)
        {
            if (!result.Succeeded || Time.time < nextActionTime)
            {
                ConfirmActivation(false);
                return;
            }

            if (!TryFindAction(result.GestureId, out var actionId))
            {
                ConfirmActivation(false);
                if (logActions)
                {
                    Debug.Log($"No action binding for gesture: {result.GestureId}");
                }

                return;
            }

            nextActionTime = Time.time + actionCooldown;
            if (logActions)
            {
                Debug.Log($"Gesture action requested: {result.GestureId} -> {actionId}");
            }

            ActionRequested?.Invoke(actionId, result);
        }

        private bool TryFindAction(string gestureId, out string actionId)
        {
            actionId = string.Empty;
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding == null)
                {
                    continue;
                }

                if (string.Equals(binding.GestureId, gestureId, StringComparison.OrdinalIgnoreCase))
                {
                    actionId = binding.ActionId;
                    return !string.IsNullOrEmpty(actionId);
                }
            }

            return false;
        }
    }
}
