using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Wapawapa.Abilities;

namespace Wapawapa.Boxing
{
    public sealed class PunchHitbox : MonoBehaviour
    {
        [SerializeField, HideInInspector] private PlayerPunchSettings punchSettings;
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private float overlapRadiusMultiplier = 1.15f;
        [SerializeField] private float minimumOverlapRadius = 0.3f;

        private readonly Dictionary<Component, float> nextHitTimes = new();
        private readonly Collider[] overlapHits = new Collider[16];
        private Transform ownerRoot;
        private NetworkObject ownerNetworkObject;
        private SphereCollider sphereCollider;
        private Vector3 previousPosition;
        private Vector3 velocity;
        private Vector3 manualPunchDirection;
        private float manualPunchEndsAt;

        private void OnEnable()
        {
            ownerRoot = transform.root;
            ownerNetworkObject = GetComponentInParent<NetworkObject>();
            sphereCollider = GetComponent<SphereCollider>();
            if (punchSettings == null)
            {
                punchSettings = GetComponentInParent<PlayerPunchSettings>();
            }

            previousPosition = transform.position;
        }

        private void Update()
        {
            if (!CanEvaluateHits())
            {
                return;
            }

            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            velocity = (transform.position - previousPosition) / deltaTime;
            previousPosition = transform.position;

            ScanOverlaps();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!CanEvaluateHits())
            {
                return;
            }

            TryHit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!CanEvaluateHits())
            {
                return;
            }

            TryHit(other);
        }

        private bool CanEvaluateHits()
        {
            return ownerNetworkObject == null || !ownerNetworkObject.IsValid || ownerNetworkObject.HasStateAuthority;
        }

        private void ScanOverlaps()
        {
            if (punchSettings == null)
            {
                return;
            }

            var hasManualPunch = Time.time <= manualPunchEndsAt;
            if (!hasManualPunch && velocity.magnitude < punchSettings.MinimumHitSpeed)
            {
                return;
            }

            var radius = GetWorldRadius();
            var count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlapHits, hitMask, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                TryHit(overlapHits[i]);
                overlapHits[i] = null;
            }
        }

        private float GetWorldRadius()
        {
            var baseRadius = sphereCollider != null ? sphereCollider.radius : 0.5f;
            var scale = transform.lossyScale;
            var maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var scaledRadius = baseRadius * Mathf.Max(0.01f, maxScale) * overlapRadiusMultiplier;
            return Mathf.Max(minimumOverlapRadius, scaledRadius);
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
