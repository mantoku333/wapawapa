using UnityEngine;

namespace Wapawapa.Abilities
{
    public readonly struct AbilityDamage
    {
        public AbilityDamage(string abilityId, float amount, Vector3 direction, float pushForce, Vector3 point, GameObject source)
        {
            AbilityId = abilityId;
            Amount = amount;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            PushForce = pushForce;
            Point = point;
            Source = source;
        }

        public string AbilityId { get; }
        public float Amount { get; }
        public Vector3 Direction { get; }
        public float PushForce { get; }
        public Vector3 Point { get; }
        public GameObject Source { get; }
    }
}
