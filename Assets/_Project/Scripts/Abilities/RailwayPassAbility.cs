using System.Collections.Generic;
using UnityEngine;

namespace Wapawapa.Abilities
{
    public sealed class RailwayPassAbility : AbilityBase
    {
        [Header("Visual")]
        [Tooltip("見た目として表示する電車Prefabです。未設定の場合は緑の仮キューブを表示します。")]
        [SerializeField] private GameObject trainPrefab;

        [Tooltip("電車Prefabの見た目だけをローカル位置でずらします。浮く場合はYをマイナスにしてください。")]
        [SerializeField] private Vector3 trainPrefabLocalPosition = new Vector3(0f, -0.75f, 0f);

        [Tooltip("電車Prefabの見た目だけのスケールです。")]
        [SerializeField] private Vector3 trainPrefabScale = Vector3.one;

        [Tooltip("電車Prefabの見た目だけの回転補正です。進行方向と見た目が合わない場合に調整します。")]
        [SerializeField] private Vector3 trainPrefabEulerAngles = new Vector3(0f, -90f, 0f);

        [Header("Train Movement")]
        [Tooltip("視線の先、何m地点を電車が通るかです。")]
        [Min(0f)]
        [SerializeField] private float targetDistance = 10f;

        [Tooltip("目標地点の右側、何m離れた場所から出発するかです。大きいほど助走が長くなります。")]
        [Min(0f)]
        [SerializeField] private float spawnRightOffset = 14f;

        [Tooltip("電車の移動速度です。Inspectorでここを変えると速さが変わります。")]
        [Min(0.1f)]
        [SerializeField] private float trainSpeed = 300f;

        [Tooltip("発動後、何秒で電車を消すかです。")]
        [Min(0.1f)]
        [SerializeField] private float lifetime = 10f;

        [Tooltip("攻撃判定のサイズです。見た目ではなく当たり判定に使います。")]
        [SerializeField] private Vector3 trainSize = new Vector3(2f, 2f, 8f);

        [Tooltip("電車の本体位置の高さです。浮く場合は小さくしてください。")]
        [SerializeField] private float heightOffset = 0.05f;

        [Header("Attack")]
        [Min(0f)]
        [SerializeField] private float damage = 30f;

        [Min(0f)]
        [SerializeField] private float pushForce = 12f;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Sound")]
        [SerializeField] private AudioClip trainSound;
        [SerializeField] private AudioClip hornSound;
        [Min(0.1f)]
        [SerializeField] private float soundDuration = 2.5f;

        [Min(0f)]
        [SerializeField] private float soundVolume = 0.85f;

        [Min(0f)]
        [SerializeField] private float hornVolume = 1.25f;

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

            var train = new GameObject("Railway Pass Train");
            train.name = "Railway Pass Train";
            train.transform.SetPositionAndRotation(spawnPoint, Quaternion.LookRotation(travelDirection, Vector3.up));

            CreateTrainVisual(train.transform);

            var collider = train.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = trainSize;

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
                trainSize,
                hitMask);

            PlayHornSound(spawnPoint);
            PlayTrainSound(spawnPoint);
        }

        private void CreateTrainVisual(Transform parent)
        {
            if (trainPrefab != null)
            {
                var visual = Instantiate(trainPrefab, parent);
                visual.name = trainPrefab.name;
                visual.transform.localPosition = trainPrefabLocalPosition;
                visual.transform.localRotation = Quaternion.Euler(trainPrefabEulerAngles);
                visual.transform.localScale = trainPrefabScale;
                return;
            }

            var fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.name = "Fallback Train Cube";
            fallback.transform.SetParent(parent, false);
            fallback.transform.localScale = trainSize;

            var fallbackCollider = fallback.GetComponent<Collider>();
            if (fallbackCollider != null)
            {
                Destroy(fallbackCollider);
            }

            var renderer = fallback.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.05f, 0.65f, 0.2f);
            }
        }

        private void PlayTrainSound(Vector3 position)
        {
            if (trainSound != null)
            {
                AudioSource.PlayClipAtPoint(trainSound, position, soundVolume);
                return;
            }

            var clip = CreateTrainClip(soundDuration);
            AudioSource.PlayClipAtPoint(clip, position, soundVolume);
            Destroy(clip, soundDuration + 0.25f);
        }

        private void PlayHornSound(Vector3 position)
        {
            if (hornSound == null)
            {
                return;
            }

            AudioSource.PlayClipAtPoint(hornSound, position, hornVolume);
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
        private Vector3 hitboxSize;
        private LayerMask hitMask;

        public void Initialize(
            string abilityId,
            GameObject owner,
            Vector3 travelDirection,
            float speed,
            float lifetime,
            float damage,
            float pushForce,
            Vector3 hitboxSize,
            LayerMask hitMask)
        {
            this.abilityId = abilityId;
            this.owner = owner;
            this.travelDirection = travelDirection.sqrMagnitude > 0f ? travelDirection.normalized : Vector3.left;
            this.speed = Mathf.Max(0.1f, speed);
            this.destroyTime = Time.time + Mathf.Max(0.1f, lifetime);
            this.damage = damage;
            this.pushForce = pushForce;
            this.hitboxSize = hitboxSize;
            this.hitMask = hitMask;
        }

        private void Update()
        {
            var startPosition = transform.position;
            var distance = speed * Time.deltaTime;
            transform.position += travelDirection * distance;
            ApplyDamageAlongPath(startPosition, distance);

            if (Time.time >= destroyTime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryApplyDamage(other, transform.position);
        }

        private void ApplyDamageAlongPath(Vector3 startPosition, float distance)
        {
            var center = startPosition + travelDirection * (distance * 0.5f);
            var halfExtents = new Vector3(
                Mathf.Max(0.01f, hitboxSize.x * 0.5f),
                Mathf.Max(0.01f, hitboxSize.y * 0.5f),
                Mathf.Max(0.01f, hitboxSize.z * 0.5f + distance * 0.5f));
            var rotation = transform.rotation;
            var hits = Physics.OverlapBox(center, halfExtents, rotation, hitMask, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                TryApplyDamage(hit, center);
            }
        }

        private void TryApplyDamage(Collider other, Vector3 sourcePosition)
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

            var hitPoint = other.ClosestPoint(sourcePosition);
            var damageData = new AbilityDamage(abilityId, damage, travelDirection, pushForce, hitPoint, owner);
            AbilityDamageUtility.TryApplyDamage(other, damageData);
        }
    }
}
