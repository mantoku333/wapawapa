using UnityEngine;

namespace Wapawapa.Abilities
{
    public readonly struct AbilityActivationData
    {
        public AbilityActivationData(string abilityId, Vector3 origin, Vector3 direction, Quaternion rotation, GameObject source)
        {
            AbilityId = abilityId;
            Origin = origin;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            Rotation = rotation;
            Source = source;
        }

        public string AbilityId { get; }
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }
        public Quaternion Rotation { get; }
        public GameObject Source { get; }

        public static AbilityActivationData FromContext(string abilityId, in AbilityContext context)
        {
            var aimSource = context.AimSource;
            return new AbilityActivationData(
                abilityId,
                aimSource.position,
                aimSource.forward,
                aimSource.rotation,
                context.Owner);
        }
    }
}
