using System.Collections.Generic;
using UnityEngine;

namespace Wapawapa.Abilities
{
    public sealed class RailwayPassAbility : AbilityBase
    {
        [Header("Train Movement")]
        [SerializeField] private float targetDistance = 10f;
        [SerializeField] private float spawnRightOffset = 7f;
        [SerializeField] private float trainSpeed = 6f;
        [SerializeField] private float lifetime = 10f;
        [SerializeField] private Vector3 trainSize = new Vector3(4f, 1.6f, 1.2f);
        [SerializeField] private float heightOffset = 0.8f;

        [Header("Attack")]
        [SerializeField] private float damage = 30f;
        [SerializeField] private float pushForce = 12f;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Sound")]
        [SerializeField] private float soundDuration = 2.5f;
        [SerializeField] private float soundVolume = 0.6f;

        protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
        {
            var forward = Vector3.ProjectOnPlane(activation.Direction, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }
            forward.Normalize();

            var right = Vector3.Cross(Vector3.up, forward).normalized;
            var travelDirection = -right;
            var baseHeight = context.Owner != null ? context.Owner.transform.position.y : activation.Origin.y;
            var targetPoint = activation.Origin + forward * targetDistance;
            targetPoint.y = baseHeight + heightOffset;
            var spawnPoint = targetPoint + right * spawnRightOffset;

            var train = GameObject.CreatePrimitive(PrimitiveType.Cube);
            train.name = "Railway Pass Train";
            train.transform.SetPositionAndRotation(spawnPoint, Quaternion.LookRotation(travelDirection, Vector3.up));
            train.transform.localScale = trainSize;

            var renderer = train.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.05f, 0.65f, 0.2f);
            }

            var collider = train.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            var rigidbody = train.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            var mover = train.AddComponent<RailwayTrainMover>();
            mover.Initialize(
                activation.AbilityId,
                context.Owner,
                travelDirection,
                trainSpeed,
                lifetime,
                damage,
                pushForce,
                hitMask);

            PlayTrainSound(spawnPoint);
        }

        private void PlayTrainSound(Vector3 position)
        {
            var clip = CreateTrainClip(soundDuration);
            AudioSource.PlayClipAtPoint(clip, position, soundVolume);
            Destroy(clip, soundDuration + 0.25f);
        }

        private static AudioClip CreateTrainClip(float duration)
        {
            const int sampleRate = 22050;
            var sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * Mathf.Max(0.1f, duration)));
            var data = new float[sampleCount];

            for (var i = 0; i < sampleCount; i++)
            {
                var time = i / (float)sampleRate;
                var envelope = Mathf.Clamp01(time * 5f) * Mathf.Clamp01((duration - time) * 3f);
                var rumble = Mathf.Sin(2f * Mathf.PI * 56f * time) * 0.42f;
                var clack = Mathf.Sin(2f * Mathf.PI * 8f * time) > 0.72f ? 0.28f : -0.08f;
                var hiss = (Mathf.PerlinNoise(time * 90f, 0.31f) - 0.5f) * 0.32f;
                data[i] = (rumble + clack + hiss) * envelope;
            }

            var clip = AudioClip.Create("RailwayPassTrainSound", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    internal sealed class RailwayTrainMover : MonoBehaviour
    {
        private readonly HashSet<Transform> damagedRoots = new HashSet<Transform>();

        private string abilityId;
        private GameObject owner;
        private Vector3 travelDirection;
        private float speed;
        private float destroyTime;
        private float damage;
        private float pushForce;
        private LayerMask hitMask;

        public void Initialize(
            string abilityId,
            GameObject owner,
            Vector3 travelDirection,
            float speed,
            float lifetime,
            float damage,
            float pushForce,
            LayerMask hitMask)
        {
            this.abilityId = abilityId;
            this.owner = owner;
            this.travelDirection = travelDirection.sqrMagnitude > 0f ? travelDirection.normalized : Vector3.left;
            this.speed = Mathf.Max(0.1f, speed);
            this.destroyTime = Time.time + Mathf.Max(0.1f, lifetime);
            this.damage = damage;
            this.pushForce = pushForce;
            this.hitMask = hitMask;
        }

        private void Update()
        {
            transform.position += travelDirection * (speed * Time.deltaTime);

            if (Time.time >= destroyTime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & hitMask.value) == 0)
            {
                return;
            }

            if (owner != null && other.transform.IsChildOf(owner.transform))
            {
                return;
            }

            var root = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform.root;
            if (root != null && !damagedRoots.Add(root))
            {
                return;
            }

            var hitPoint = other.ClosestPoint(transform.position);
            var damageData = new AbilityDamage(abilityId, damage, travelDirection, pushForce, hitPoint, owner);
            AbilityDamageUtility.TryApplyDamage(other, damageData);
        }
    }
}
