using UnityEngine;
using UnityEngine.Events;
using Wapawapa.Abilities;

namespace Wapawapa.Boxing
{
    public sealed class BoxingTarget : MonoBehaviour, IAbilityDamageReceiver
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private Rigidbody targetRigidbody;
        [SerializeField] private UnityEvent onKnockedOut;

        private float health;

        public float Health => health;
        public float MaxHealth => maxHealth;

        private void Awake()
        {
            health = maxHealth;
            if (targetRigidbody == null)
            {
                targetRigidbody = GetComponent<Rigidbody>();
            }
        }

        public void ApplyHit(BoxingHit hit)
        {
            health = Mathf.Max(0f, health - hit.Damage);

            if (targetRigidbody != null && hit.PushForce > 0f)
            {
                targetRigidbody.AddForceAtPosition(hit.Direction * hit.PushForce, hit.Point, ForceMode.Impulse);
            }

            Debug.Log($"{name} took {hit.Damage:0} damage. HP {health:0}/{maxHealth:0}");

            if (health <= 0f)
            {
                onKnockedOut?.Invoke();
            }
        }

        public void ApplyDamage(in AbilityDamage damage)
        {
            ApplyHit(new BoxingHit(damage.Amount, damage.Direction, damage.PushForce, damage.Point, damage.Source, damage.AbilityId));
        }

        public void ResetHealth()
        {
            health = maxHealth;
        }
    }
}
