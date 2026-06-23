using UnityEngine;

namespace Wapawapa.Abilities
{
    public static class AbilityDamageUtility
    {
        public static bool TryApplyDamage(Collider collider, in AbilityDamage damage)
        {
            if (collider == null)
            {
                return false;
            }

            if (collider.TryGetComponent(out IAbilityDamageReceiver receiver))
            {
                receiver.ApplyDamage(damage);
                return true;
            }

            var parentReceiver = collider.GetComponentInParent<IAbilityDamageReceiver>();
            if (parentReceiver == null)
            {
                return false;
            }

            parentReceiver.ApplyDamage(damage);
            return true;
        }
    }
}
