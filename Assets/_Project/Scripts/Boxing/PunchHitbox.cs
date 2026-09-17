using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.XR;
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
        private Transform head;
        private XRNode? hapticHand;
        private Vector3 strokeDirection;
        private Vector3 strokeStart;
        private float peakExtension;
        private bool waitingForRetraction;
        private bool punchActive;
        private bool punchHit;
        private bool manualPunch;
        private bool sampleReady;
        private int sampledFrame = -1;

        private void OnEnable()
        {
            ownerRoot = transform.root;
            ownerNetworkObject = GetComponentInParent<NetworkObject>();
            sphereCollider = GetComponent<SphereCollider>();
            if (punchSettings == null)
            {
                punchSettings = GetComponentInParent<PlayerPunchSettings>();
            }

            // Existing prefabs already contain these named rig children; no scene wiring needed.
            ownerRoot = punchSettings != null ? punchSettings.transform : ownerRoot;
            head = ownerRoot.Find("Head");
            hapticHand = null;
            for (var current = transform; current != null && current != ownerRoot; current = current.parent)
            {
                if (current.name == "LeftHand") hapticHand = XRNode.LeftHand;
                if (current.name == "RightHand") hapticHand = XRNode.RightHand;
            }

            sampleReady = false;
            punchActive = punchHit = manualPunch = waitingForRetraction = false;
            manualPunchEndsAt = float.NegativeInfinity;
            sampledFrame = -1;
            nextHitTimes.Clear();
        }

        private void LateUpdate()
        {
            if (!CanEvaluateHits() || punchSettings == null)
            {
                sampleReady = false;
                return;
            }

            // Sample after rig Update: subtract head translation and root rotation so
            // locomotion and turning do not become a punch. Head rotation is not applied
            // to positions, so simply looking around does not move the sampled hand.
            var position = GetRelativePosition();
            if (!sampleReady)
            {
                previousPosition = strokeStart = position;
                sampleReady = true;
                punchActive = false;
            }
            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            var relativeVelocity = (position - previousPosition) / deltaTime;
            velocity = ownerRoot.TransformDirection(relativeVelocity);
            previousPosition = position;
            UpdatePunch(position, relativeVelocity);
            sampledFrame = Time.frameCount;

            ScanOverlaps();
        }

        private Vector3 GetRelativePosition()
        {
            var origin = head != null ? head.position : ownerRoot.position;
            return ownerRoot.InverseTransformDirection(transform.position - origin);
        }

        private void UpdatePunch(Vector3 position, Vector3 relativeVelocity)
        {
            if (manualPunch)
            {
                peakExtension = Mathf.Max(peakExtension, Vector3.Dot(position - strokeStart, strokeDirection));
                punchActive = Time.time < manualPunchEndsAt;
                if (punchActive) return;
                manualPunch = false;
            }

            var forward = ownerRoot.InverseTransformDirection(head != null ? head.forward : ownerRoot.forward);
            var speed = relativeVelocity.magnitude;
            var isForward = speed >= Mathf.Max(0.01f, punchSettings.MinimumHitSpeed)
                && Vector3.Dot(relativeVelocity.normalized, forward) >= punchSettings.MinimumForwardDot;

            if (waitingForRetraction)
            {
                var extension = Vector3.Dot(position - strokeStart, strokeDirection);
                peakExtension = Mathf.Max(peakExtension, extension);
                // Once the outward stroke ends, it cannot reopen just by shaking forward.
                punchActive = punchActive && isForward
                    && Vector3.Dot(relativeVelocity, strokeDirection) > 0f;
                if (peakExtension - extension >= punchSettings.PunchRetractDistance)
                {
                    waitingForRetraction = false;
                    punchActive = false;
                    strokeStart = position;
                }
                return;
            }

            if (!isForward)
            {
                strokeStart = position;
                punchActive = false;
                return;
            }

            if (Vector3.Dot(position - strokeStart, forward) < punchSettings.MinimumPunchDistance) return;
            strokeDirection = forward;
            peakExtension = Vector3.Dot(position - strokeStart, strokeDirection);
            waitingForRetraction = punchActive = true;
            punchHit = false;
            PlayerCombatAudio.PlayPunchSwing(transform.position, punchSettings.PunchSwingVolume);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!CanEvaluateHits() || sampledFrame != Time.frameCount)
            {
                return;
            }

            TryHit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!CanEvaluateHits() || sampledFrame != Time.frameCount)
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

            if (!punchActive || punchHit)
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
            if (other == null || punchSettings == null || (hitMask.value & (1 << other.gameObject.layer)) == 0)
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

            if (!punchActive || punchHit)
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

            var direction = manualPunch ? manualPunchDirection : velocity.normalized;
            var hitPoint = other.ClosestPoint(transform.position);
            // Consume before callbacks: multiple colliders/targets must not multiply a punch.
            punchHit = true;
            receiver.ApplyDamage(new AbilityDamage(
                punchSettings.PunchId,
                punchSettings.Damage,
                direction,
                punchSettings.PushForce,
                hitPoint,
                ownerRoot != null ? ownerRoot.gameObject : gameObject));
            nextHitTimes[receiverComponent] = Time.time + punchSettings.RepeatHitDelay;
            PlayHitHaptics();
        }

        private void PlayHitHaptics()
        {
            if (!hapticHand.HasValue || !CanEvaluateHits()
                || punchSettings.HitHapticAmplitude <= 0f || punchSettings.HitHapticDuration <= 0f) return;

            var device = InputDevices.GetDeviceAtXRNode(hapticHand.Value);
            if (device.isValid && device.TryGetHapticCapabilities(out var capabilities)
                && capabilities.supportsImpulse && capabilities.numChannels > 0)
            {
                device.SendHapticImpulse(0u, punchSettings.HitHapticAmplitude, punchSettings.HitHapticDuration);
            }
        }

        public void SetPunchSettings(PlayerPunchSettings settings)
        {
            punchSettings = settings;
        }

        public void StartManualPunch(Vector3 direction, float duration)
        {
            if (duration <= 0f || !CanEvaluateHits())
            {
                return;
            }

            manualPunchDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            manualPunchEndsAt = Time.time + duration;
            // Preserve desktop/test callers: each explicit request starts one punch.
            manualPunch = punchActive = waitingForRetraction = true;
            punchHit = false;
            strokeStart = GetRelativePosition();
            strokeDirection = ownerRoot.InverseTransformDirection(manualPunchDirection);
            peakExtension = 0f;
            PlayerCombatAudio.PlayPunchSwing(transform.position, punchSettings != null ? punchSettings.PunchSwingVolume : 0.8f);
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
