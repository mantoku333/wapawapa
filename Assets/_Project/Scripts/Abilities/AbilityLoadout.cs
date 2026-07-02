using System;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Wapawapa.Abilities
{
    public sealed class AbilityLoadout : NetworkBehaviour
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
        private NetworkObject networkObject;

        private void Awake()
        {
            networkObject = GetComponentInParent<NetworkObject>();
        }

        private void Update()
        {
            if (!CanReadLocalInput())
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
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
                    var context = CreateContext();
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
                        TryActivateSlot(i);
                        heldSlot = null;
                    }

                    continue;
                }

                if (keyControl.wasPressedThisFrame)
                {
                    TryActivateSlot(i);
                }
            }
        }

        private void OnDisable()
        {
            if (heldSlot != null && heldSlot.Ability is IHoldAbility holdAbility)
            {
                holdAbility.EndHold(CreateContext(), false);
                heldSlot = null;
            }
        }

        private bool CanReadLocalInput()
        {
            return networkObject == null || !networkObject.IsValid || networkObject.HasStateAuthority;
        }

        private void TryActivateSlot(int slotIndex)
        {
            if (!TryGetSlot(slotIndex, out var slot))
            {
                return;
            }

            if (!slot.Ability.IsReady)
            {
                Debug.Log($"Ability not ready: {slot.Label} ({slot.Ability.RemainingCooldown:0.0}s)");
                return;
            }

            var activation = AbilityActivationData.FromContext(slot.Ability.AbilityId, CreateContext());
            if (networkObject != null && networkObject.IsValid)
            {
                RPC_ActivateAbility(slotIndex, activation.Origin, activation.Direction, activation.Rotation);
                return;
            }

            ActivateSlot(slot, activation, true);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ActivateAbility(int slotIndex, Vector3 origin, Vector3 direction, Quaternion rotation)
        {
            if (!TryGetSlot(slotIndex, out var slot))
            {
                return;
            }

            var activation = new AbilityActivationData(slot.Ability.AbilityId, origin, direction, rotation, gameObject);
            ActivateSlot(slot, activation, false);
        }

        private bool TryGetSlot(int slotIndex, out AbilitySlot slot)
        {
            slot = null;
            if (slotIndex < 0 || slotIndex >= slots.Length)
            {
                return false;
            }

            slot = slots[slotIndex];
            return slot != null && slot.Ability != null;
        }

        private AbilityContext CreateContext()
        {
            return new AbilityContext(gameObject, head, leftHand, rightHand);
        }

        private void ActivateSlot(AbilitySlot slot, in AbilityActivationData activation, bool logNotReady)
        {
            var activated = slot.Ability.TryActivate(CreateContext(), activation);
            if (!activated && logNotReady)
            {
                Debug.Log($"Ability not ready: {slot.Label} ({slot.Ability.RemainingCooldown:0.0}s)");
            }
        }
    }
}
