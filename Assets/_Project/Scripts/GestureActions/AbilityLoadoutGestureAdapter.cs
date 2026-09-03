using System;
using UnityEngine;
using Wapawapa.Abilities;
using Wapawapa.Gestures;

namespace Wapawapa.GestureActions
{
    public sealed class AbilityLoadoutGestureAdapter : MonoBehaviour
    {
        [SerializeField] private GestureActionRouter router;
        [SerializeField] private AbilityLoadout loadout;
        [SerializeField] private GestureAbilityBinding[] bindings =
        {
            new GestureAbilityBinding("ability.slot.0", 0),
            new GestureAbilityBinding("ability.slot.1", 1),
        };
        [SerializeField] private bool logRequests;

        private void Awake()
        {
            if (loadout == null)
            {
                loadout = GetComponentInParent<AbilityLoadout>();
            }
        }

        private void OnEnable()
        {
            if (router != null)
            {
                router.ActionRequested += HandleActionRequested;
            }
        }

        private void OnDisable()
        {
            if (router != null)
            {
                router.ActionRequested -= HandleActionRequested;
            }
        }

        private void HandleActionRequested(string actionId, AirGestureResult result)
        {
            if (loadout == null)
            {
                return;
            }

            if (!TryFindSlot(actionId, out var slotIndex))
            {
                if (logRequests)
                {
                    Debug.Log($"No ability binding for gesture action: {actionId}");
                }

                return;
            }

            var accepted = loadout.RequestActivateSlot(slotIndex);
            if (logRequests)
            {
                Debug.Log($"Gesture ability request: {result.GestureId} -> slot {slotIndex}, accepted={accepted}");
            }
        }

        private bool TryFindSlot(string actionId, out int slotIndex)
        {
            slotIndex = -1;
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding == null)
                {
                    continue;
                }

                if (string.Equals(binding.ActionId, actionId, StringComparison.OrdinalIgnoreCase))
                {
                    slotIndex = binding.SlotIndex;
                    return slotIndex >= 0;
                }
            }

            return false;
        }
    }
}
