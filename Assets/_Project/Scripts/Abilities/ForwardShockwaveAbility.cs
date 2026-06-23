using UnityEngine;
using Wapawapa.Boxing;

namespace Wapawapa.Abilities
{
    public sealed class ForwardShockwaveAbility : AbilityBase
    {
        [Header("Shockwave")]
        [SerializeField] private float range = 4f;
        [SerializeField] private float radius = 1.25f;
        [SerializeField] private float damage = 25f;
        [SerializeField] private float pushForce = 8f;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private GameObject effectPrefab;

        protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
        {
            var origin = activation.Origin + activation.Direction * 0.6f;
            var center = origin + activation.Direction * range * 0.5f;

            if (effectPrefab != null)
            {
                var effect = Instantiate(effectPrefab, origin, Quaternion.LookRotation(activation.Direction, Vector3.up));
                Destroy(effect, 2f);
            }

            var hits = Physics.OverlapSphere(center, radius, hitMask, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                if (context.Owner != null && hit.transform.IsChildOf(context.Owner.transform))
                {
                    continue;
                }

                var direction = (hit.transform.position - origin).normalized;
                var damageData = new AbilityDamage(activation.AbilityId, damage, direction, pushForce, hit.ClosestPoint(origin), context.Owner);
                AbilityDamageUtility.TryApplyDamage(hit, damageData);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position + transform.forward * (range * 0.5f + 0.6f), radius);
        }
    }
}
