using UnityEngine;
using UnityEngine.Events;

namespace Wapawapa.Abilities
{
    public sealed class PlayerDamageReceiver : MonoBehaviour, IAbilityDamageReceiver
    {
        [Header("プレイヤー体力")]
        [Tooltip("プレイヤーの最大体力です。パンチやアビリティのダメージで減ります。")]
        [SerializeField] private float maxHealth = 100f;

        [Tooltip("体力が0になった時に呼ばれるイベントです。演出やリスポーン処理を後から接続できます。")]
        [SerializeField] private UnityEvent onKnockedOut;

        private float health;

        public float Health => health;
        public float MaxHealth => maxHealth;

        private void Awake()
        {
            health = maxHealth;
        }

        public void ApplyDamage(in AbilityDamage damage)
        {
            health = Mathf.Max(0f, health - damage.Amount);
            Debug.Log($"{name} took {damage.Amount:0} ability damage from {damage.AbilityId}. HP {health:0}/{maxHealth:0}");

            if (health <= 0f)
            {
                onKnockedOut?.Invoke();
            }
        }

        public void ResetHealth()
        {
            health = maxHealth;
        }
    }
}
