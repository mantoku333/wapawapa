using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Wapawapa.Abilities
{
    public sealed class AbilityLoadout : MonoBehaviour
    {
        [Serializable]
        private sealed class AbilitySlot
        {
            [SerializeField] private string label = "Ability";
            [SerializeField] private Key activationKey = Key.Digit1;
            [SerializeField] private AbilityBase ability;

            public string Label => label;
            public Key ActivationKey => activationKey;
            public AbilityBase Ability => ability;
        }

        [Header("Rig References")]
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        [Header("Slots")]
        [SerializeField] private AbilitySlot[] slots = Array.Empty<AbilitySlot>();

        private AbilitySlot heldSlot;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var context = new AbilityContext(gameObject, head, leftHand, rightHand);
            foreach (var slot in slots)
            {
                if (slot == null || slot.Ability == null)
                {
                    continue;
                }

                var keyControl = keyboard[slot.ActivationKey];
                if (keyControl == null)
                {
                    continue;
                }

                if (slot.Ability is IHoldAbility holdAbility)
                {
                    if (keyControl.wasPressedThisFrame)
                    {
                        if (slot.Ability.IsReady)
                        {
                            heldSlot = slot;
                            holdAbility.BeginHold(context);
                        }
                        else
                        {
                            Debug.Log($"Ability not ready: {slot.Label} ({slot.Ability.RemainingCooldown:0.0}s)");
                        }
                    }

                    if (heldSlot == slot && keyControl.isPressed)
                    {
                        holdAbility.UpdateHold(context);
                    }

                    if (heldSlot == slot && keyControl.wasReleasedThisFrame)
                    {
                        holdAbility.EndHold(context, true);
                        var activated = slot.Ability.TryActivate(context);
                        if (!activated)
                        {
                            Debug.Log($"Ability not ready: {slot.Label} ({slot.Ability.RemainingCooldown:0.0}s)");
                        }

                        heldSlot = null;
                    }

                    continue;
                }

                if (keyControl.wasPressedThisFrame)
                {
                    var activated = slot.Ability.TryActivate(context);
                    if (!activated)
                    {
                        Debug.Log($"Ability not ready: {slot.Label} ({slot.Ability.RemainingCooldown:0.0}s)");
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (heldSlot != null && heldSlot.Ability is IHoldAbility holdAbility)
            {
                var context = new AbilityContext(gameObject, head, leftHand, rightHand);
                holdAbility.EndHold(context, false);
                heldSlot = null;
            }
        }
    }
}
