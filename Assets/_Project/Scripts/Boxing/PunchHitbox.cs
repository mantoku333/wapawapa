using System.Collections.Generic;
using UnityEngine;
using Wapawapa.Abilities;

namespace Wapawapa.Boxing
{
    public sealed class PunchHitbox : MonoBehaviour
    {
        [SerializeField, HideInInspector] private PlayerPunchSettings punchSettings;

        private readonly Dictionary<Component, float> nextHitTimes = new();
        private Transform ownerRoot;
        private Vector3 previousPosition;
        private Vector3 velocity;
        private Vector3 manualPunchDirection;
        private float manualPunchEndsAt;

        private void OnEnable()
        {
            ownerRoot = transform.root;
            if (punchSettings == null)
            {
                punchSettings = GetComponentInParent<PlayerPunchSettings>();
            }

            previousPosition = transform.position;
        }

        private void Update()
        {
            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            velocity = (transform.position - previousPosition) / deltaTime;
            previousPosition = transform.position;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryHit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryHit(other);
        }

        private void TryHit(Collider other)
        {
            if (other == null || punchSettings == null)
            {
                return;
            }

            if (punchSettings.IgnoreSelfHits && other.transform.IsChildOf(ownerRoot))
            {
                return;
            }

            if (punchSettings.IgnoreHandToHandHits && other.GetComponentInParent<PunchHitbox>() != null)
            {
                return;
            }

            var hasManualPunch = Time.time <= manualPunchEndsAt;
            if (!hasManualPunch && velocity.magnitude < punchSettings.MinimumHitSpeed)
            {
                return;
            }

            if (!TryGetReceiver(other, out var receiver, out var receiverComponent))
            {
                return;
            }

            if (nextHitTimes.TryGetValue(receiverComponent, out var nextTime) && Time.time < nextTime)
            {
                return;
            }

            var direction = hasManualPunch ? manualPunchDirection : velocity.normalized;
            var hitPoint = other.ClosestPoint(transform.position);
            receiver.ApplyDamage(new AbilityDamage(
                punchSettings.PunchId,
                punchSettings.Damage,
                direction,
                punchSettings.PushForce,
                hitPoint,
                ownerRoot != null ? ownerRoot.gameObject : gameObject));
            nextHitTimes[receiverComponent] = Time.time + punchSettings.RepeatHitDelay;
        }

        public void SetPunchSettings(PlayerPunchSettings settings)
        {
            punchSettings = settings;
        }

        public void StartManualPunch(Vector3 direction, float duration)
        {
            if (duration <= 0f)
            {
                return;
            }

            manualPunchDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            manualPunchEndsAt = Time.time + duration;
        }

        private static bool TryGetReceiver(Collider other, out IAbilityDamageReceiver receiver, out Component receiverComponent)
        {
            if (other.TryGetComponent(out receiver))
            {
                receiverComponent = (Component)receiver;
                return true;
            }

            receiver = other.GetComponentInParent<IAbilityDamageReceiver>();
            receiverComponent = receiver as Component;
            return receiverComponent != null;
        }
    }
}
