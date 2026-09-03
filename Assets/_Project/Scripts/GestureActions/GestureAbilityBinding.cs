using System;
using UnityEngine;

namespace Wapawapa.GestureActions
{
    [Serializable]
    public sealed class GestureAbilityBinding
    {
        [SerializeField] private string actionId = "ability.slot.0";
        [SerializeField] private int slotIndex;

        public GestureAbilityBinding()
        {
        }

        public GestureAbilityBinding(string actionId, int slotIndex)
        {
            this.actionId = actionId;
            this.slotIndex = slotIndex;
        }

        public string ActionId => actionId;
        public int SlotIndex => slotIndex;
    }
}
