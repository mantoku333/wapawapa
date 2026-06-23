using UnityEngine;

namespace Wapawapa.Abilities
{
    public readonly struct AbilityContext
    {
        public AbilityContext(GameObject owner, Transform head, Transform leftHand, Transform rightHand)
        {
            Owner = owner;
            Head = head;
            LeftHand = leftHand;
            RightHand = rightHand;
        }

        public GameObject Owner { get; }
        public Transform Head { get; }
        public Transform LeftHand { get; }
        public Transform RightHand { get; }

        public Transform AimSource => Head != null ? Head : Owner.transform;
    }
}
