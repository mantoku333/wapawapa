using UnityEngine;

namespace Wapawapa.Boxing
{
    public readonly struct BoxingHit
    {
        public BoxingHit(float damage, Vector3 direction, float pushForce, Vector3 point, GameObject source, string abilityId = "")
        {
            Damage = damage;
            Direction = direction;
            PushForce = pushForce;
            Point = point;
            Source = source;
            AbilityId = abilityId;
        }

        public string AbilityId { get; }
        public float Damage { get; }
        public Vector3 Direction { get; }
        public float PushForce { get; }
        public Vector3 Point { get; }
        public GameObject Source { get; }
    }
}
