using System;
using UnityEngine;

namespace Wapawapa.GestureActions
{
    [Serializable]
    public sealed class GestureActionBinding
    {
        [SerializeField] private string gestureId = "circle";
        [SerializeField] private string actionId = "ability.slot.0";

        public GestureActionBinding()
        {
        }

        public GestureActionBinding(string gestureId, string actionId)
        {
            this.gestureId = gestureId;
            this.actionId = actionId;
        }

        public string GestureId => gestureId;
        public string ActionId => actionId;
    }
}
