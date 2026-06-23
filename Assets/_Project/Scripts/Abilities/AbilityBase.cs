using UnityEngine;

namespace Wapawapa.Abilities
{
    public abstract class AbilityBase : MonoBehaviour
    {
        [Header("Ability")]
        [SerializeField] private string abilityId = "sample.ability";
        [SerializeField] private string abilityName = "New Ability";
        [SerializeField] private float cooldownSeconds = 1f;

        private float nextReadyTime;

        public string AbilityId => abilityId;
        public string AbilityName => abilityName;
        public float CooldownSeconds => cooldownSeconds;
        public float RemainingCooldown => Mathf.Max(0f, nextReadyTime - Time.time);
        public bool IsReady => Time.time >= nextReadyTime;

        public bool TryActivate(in AbilityContext context)
        {
            if (!enabled || !IsReady)
            {
                return false;
            }

            var activation = AbilityActivationData.FromContext(abilityId, context);
            Activate(context, activation);
            nextReadyTime = Time.time + cooldownSeconds;
            Debug.Log($"Ability activated: {abilityName} ({abilityId})");
            return true;
        }

        protected abstract void Activate(in AbilityContext context, in AbilityActivationData activation);
    }
}
