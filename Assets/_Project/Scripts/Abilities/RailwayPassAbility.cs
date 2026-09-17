using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Wapawapa.Abilities
{
    public sealed class RailwayPassAbility : AbilityBase
    {
        [Serializable]
        private class VehicleVariant
        {
            [InspectorName("種類名")]
            [SerializeField] private string label = "Vehicle";
            [Tooltip("見た目として表示するPrefabまたはモデルです。未設定の場合は緑の仮キューブを表示します。")]
            [InspectorName("モデルPrefab")]
            [SerializeField] private GameObject prefab;
            [Tooltip("見た目だけをローカル位置でずらします。浮く場合はYをマイナスにしてください。")]
            [InspectorName("モデル位置補正")]
            [SerializeField] private Vector3 localPosition = Vector3.zero;
            [Tooltip("見た目だけのスケールです。")]
            [InspectorName("モデルサイズ補正")]
            [SerializeField] private Vector3 localScale = Vector3.one;
            [Tooltip("見た目だけの回転補正です。進行方向と見た目が合わない場合に調整します。")]
            [InspectorName("モデル回転補正")]
            [SerializeField] private Vector3 localEulerAngles = Vector3.zero;
            [Tooltip("この種類だけ出現位置の横オフセットを変える場合にONにします。")]
            [InspectorName("出現位置を個別設定する")]
            [SerializeField] private bool overrideSpawnRightOffset;
            [Tooltip("この種類専用の出現位置です。大きいほど右奥から出ます。")]
            [Min(0f)]
            [InspectorName("個別の横オフセット")]
            [SerializeField] private float spawnRightOffset = 14f;
            [Tooltip("スポーンしてから発車するまでの待機中に鳴らす踏切SEです。")]
            [InspectorName("踏切SE")]
            [SerializeField] private AudioClip preLaunchSound;
            [Tooltip("発射時に鳴らす音です。")]
            [InspectorName("発射音")]
            [SerializeField] private AudioClip launchSound;
            [Tooltip("プレイヤーなどに衝突した時に鳴らす音です。")]
            [InspectorName("衝突音")]
            [SerializeField] private AudioClip hitSound;
            [Tooltip("プレイヤーなどに衝突した時に出すVFXです。水しぶきなどを設定できます。")]
            [InspectorName("衝突VFX")]
            [SerializeField] private GameObject hitEffectPrefab;
            [Tooltip("衝突地点の周りに出すVFXの個数です。")]
            [Min(0)]
            [InspectorName("衝突VFX数")]
            [SerializeField] private int hitEffectCount = 1;
            [Tooltip("衝突地点からどれくらい散らして出すかです。")]
            [Min(0f)]
            [InspectorName("衝突VFX散らばり")]
            [SerializeField] private float hitEffectRadius = 0.8f;
            [Tooltip("衝突VFXの大きさです。")]
            [Min(0f)]
            [InspectorName("衝突VFXサイズ")]
            [SerializeField] private float hitEffectScale = 1f;
            [Tooltip("生成した衝突VFXを何秒後に消すかです。")]
            [Min(0.1f)]
            [InspectorName("衝突VFX寿命")]
            [SerializeField] private float hitEffectLifetime = 3f;

            public virtual string Label => string.IsNullOrWhiteSpace(label) ? "Vehicle" : label;
            public virtual GameObject Prefab => prefab;
            public virtual Vector3 LocalPosition => localPosition;
            public virtual Vector3 LocalScale => localScale;
            public virtual Vector3 LocalEulerAngles => localEulerAngles;
            public virtual bool OverrideSpawnRightOffset => overrideSpawnRightOffset;
            public virtual float SpawnRightOffset => Mathf.Max(0f, spawnRightOffset);
            public virtual AudioClip PreLaunchSound => preLaunchSound;
            public virtual AudioClip LaunchSound => launchSound;
            public virtual AudioClip HitSound => hitSound;
            public virtual GameObject HitEffectPrefab => hitEffectPrefab;
            public virtual int HitEffectCount => Mathf.Max(0, hitEffectCount);
            public virtual float HitEffectRadius => Mathf.Max(0f, hitEffectRadius);
            public virtual float HitEffectScale => Mathf.Max(0f, hitEffectScale);
            public virtual float HitEffectLifetime => Mathf.Max(0.1f, hitEffectLifetime);
            public virtual bool HasAnyContent => prefab != null || preLaunchSound != null || launchSound != null || hitSound != null || hitEffectPrefab != null;
        }

        [Header("Visual")]
        [Tooltip("発動時にこの中からランダムで1種類を選びます。")]
        [InspectorName("ランダム出現する乗り物リスト")]
        [SerializeField] private VehicleVariant[] vehicleVariants = Array.Empty<VehicleVariant>();

        [Header("旧設定 / リスト未設定時の予備")]
        [Tooltip("見た目として表示する電車Prefabです。未設定の場合は緑の仮キューブを表示します。")]
        [InspectorName("予備モデルPrefab")]
        [SerializeField] private GameObject trainPrefab;

        [Tooltip("電車Prefabの見た目だけをローカル位置でずらします。浮く場合はYをマイナスにしてください。")]
        [InspectorName("予備モデル位置補正")]
        [SerializeField] private Vector3 trainPrefabLocalPosition = Vector3.zero;

        [Tooltip("電車Prefabの見た目だけのスケールです。")]
        [InspectorName("予備モデルサイズ補正")]
        [SerializeField] private Vector3 trainPrefabScale = Vector3.one;

        [Tooltip("電車Prefabの見た目だけの回転補正です。進行方向と見た目が合わない場合に調整します。")]
        [InspectorName("予備モデル回転補正")]
        [SerializeField] private Vector3 trainPrefabEulerAngles = Vector3.zero;

        [Header("移動")]
        [Tooltip("視線の先、何m地点を電車が通るかです。")]
        [Min(0f)]
        [InspectorName("通過地点の距離")]
        [SerializeField] private float targetDistance = 10f;

        [Tooltip("目標地点の右側、何m離れた場所から出発するかです。大きいほど助走が長くなります。")]
        [Min(0f)]
        [InspectorName("出現位置の横オフセット")]
        [SerializeField] private float spawnRightOffset = 14f;

        [Tooltip("電車の移動速度です。Inspectorでここを変えると速さが変わります。")]
        [Min(0.1f)]
        [InspectorName("移動速度")]
        [SerializeField] private float trainSpeed = 300f;

        [Tooltip("発動後、何秒で電車を消すかです。")]
        [Min(0.1f)]
        [InspectorName("消えるまでの時間")]
        [SerializeField] private float lifetime = 10f;

        [Tooltip("攻撃判定のサイズです。見た目ではなく当たり判定に使います。")]
        [InspectorName("攻撃判定サイズ")]
        [SerializeField] private Vector3 trainSize = new Vector3(2f, 2f, 8f);

        [Tooltip("電車の本体位置の高さです。浮く場合は小さくしてください。")]
        [InspectorName("本体の高さ")]
        [SerializeField] private float heightOffset = 0.05f;

        [Header("攻撃")]
        [Min(0f)]
        [InspectorName("ダメージ")]
        [SerializeField] private float damage = 30f;

        [Min(0f)]
        [InspectorName("吹き飛ばし力")]
        [SerializeField] private float pushForce = 12f;
        [InspectorName("当たるレイヤー")]
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("旧サウンド / リスト未設定時の予備")]
        [InspectorName("予備発射音")]
        [SerializeField] private AudioClip trainSound;
        [InspectorName("予備踏切SE")]
        [SerializeField] private AudioClip hornSound;
        [InspectorName("予備衝突音")]
        [SerializeField] private AudioClip hitSound;
        [Min(0f)]
        [InspectorName("発車までの待ち時間")]
        [SerializeField] private float preLaunchDelay = 2f;
        [Min(0.1f)]
        [InspectorName("自動生成音の長さ")]
        [SerializeField] private float soundDuration = 2.5f;

        [Min(0f)]
        [InspectorName("発射音量")]
        [SerializeField] private float soundVolume = 0.85f;

        [SerializeField, Range(0f, 1f)] private float soundSpatialBlend = 0.15f;

        [Min(0f)]
        [InspectorName("音の最小距離")]
        [SerializeField] private float soundMinDistance = 8f;

        [Min(0.01f)]
        [InspectorName("音の最大距離")]
        [SerializeField] private float soundMaxDistance = 80f;

        [Min(0f)]
        [InspectorName("踏切SE音量")]
        [SerializeField] private float hornVolume = 1.25f;

        [Min(0f)]
        [InspectorName("衝突音量")]
        [SerializeField] private float trainHitSoundVolume = 1f;

        protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
        {
            var variant = SelectVariant();
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
            var resolvedSpawnRightOffset = variant.OverrideSpawnRightOffset ? variant.SpawnRightOffset : spawnRightOffset;
            var spawnPoint = targetPoint + right * resolvedSpawnRightOffset;

            var train = CreateWaitingVehicle(variant, spawnPoint, travelDirection);
            if (variant.PreLaunchSound != null)
            {
                PlayConfiguredClip(variant.PreLaunchSound, spawnPoint, hornVolume, preLaunchDelay);
            }

            if (preLaunchDelay > 0f)
            {
                StartCoroutine(LaunchVehicleAfterDelay(
                    train,
                    variant,
                    activation.AbilityId,
                    context.Owner,
                    travelDirection,
                    preLaunchDelay));
                return;
            }

            LaunchVehicle(train, variant, activation.AbilityId, context.Owner, travelDirection);
        }

        private GameObject CreateWaitingVehicle(
            VehicleVariant variant,
            Vector3 spawnPoint,
            Vector3 travelDirection)
        {
            var train = new GameObject("Railway Pass Train");
            train.name = $"Railway Pass {variant.Label}";
            train.transform.SetPositionAndRotation(spawnPoint, Quaternion.LookRotation(travelDirection, Vector3.up));

            CreateTrainVisual(train.transform, variant);
            return train;
        }

        private void LaunchVehicle(
            GameObject train,
            VehicleVariant variant,
            string abilityId,
            GameObject owner,
            Vector3 travelDirection)
        {
            if (train == null)
            {
                return;
            }

            var collider = train.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = trainSize;

            var rigidbody = train.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            var mover = train.AddComponent<RailwayTrainMover>();
            mover.Initialize(
                abilityId,
                owner,
                travelDirection,
                trainSpeed,
                lifetime,
                damage,
                pushForce,
                trainSize,
                hitMask,
                variant.HitSound != null ? variant.HitSound : hitSound,
                soundSpatialBlend,
                soundMinDistance,
                soundMaxDistance,
                trainHitSoundVolume,
                variant.HitEffectPrefab,
                variant.HitEffectCount,
                variant.HitEffectRadius,
                variant.HitEffectScale,
                variant.HitEffectLifetime);

            PlayLaunchSound(train, variant);
        }

        private IEnumerator LaunchVehicleAfterDelay(
            GameObject train,
            VehicleVariant variant,
            string abilityId,
            GameObject owner,
            Vector3 travelDirection,
            float delay)
        {
            yield return new WaitForSeconds(delay);
            LaunchVehicle(train, variant, abilityId, owner, travelDirection);
        }

        private VehicleVariant SelectVariant()
        {
            if (vehicleVariants != null && vehicleVariants.Length > 0)
            {
                var validVariants = new List<VehicleVariant>();
                foreach (var variant in vehicleVariants)
                {
                    if (variant != null && variant.HasAnyContent)
                    {
                        validVariants.Add(variant);
                    }
                }

                if (validVariants.Count > 0)
                {
                    return validVariants[UnityEngine.Random.Range(0, validVariants.Count)];
                }
            }

            return new VehicleVariantFallback(
                trainPrefab,
                trainPrefabLocalPosition,
                trainPrefabScale,
                trainPrefabEulerAngles,
                false,
                0f,
                hornSound,
                trainSound,
                hitSound);
        }

        private void CreateTrainVisual(Transform parent, VehicleVariant variant)
        {
            if (variant.Prefab != null)
            {
                var visual = Instantiate(variant.Prefab, parent);
                visual.name = variant.Prefab.name;
                visual.transform.localPosition = variant.LocalPosition;
                visual.transform.localRotation = Quaternion.Euler(variant.LocalEulerAngles);
                visual.transform.localScale = variant.LocalScale;
                RepairVisualMaterials(visual);

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

        private static void RepairVisualMaterials(GameObject visual)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                return;
            }

            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.materials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null || material.shader == urpLit)
                    {
                        continue;
                    }

                    var mainTexture = FindBestMainTexture(material);
                    var color = mainTexture != null
                        ? Color.white
                        : material.HasProperty("_BaseColor")
                            ? material.GetColor("_BaseColor")
                            : material.HasProperty("_Color")
                                ? material.GetColor("_Color")
                                : Color.white;

                    material.shader = urpLit;
                    if (mainTexture != null)
                    {
                        material.SetTexture("_BaseMap", mainTexture);
                    }

                    material.SetColor("_BaseColor", color);
                }

                renderer.materials = materials;
            }
        }

        private static Texture FindBestMainTexture(Material material)
        {
            string[] textureNames =
            {
                "_BaseMap",
                "_MainTex",
                "_Diffuse",
                "_DiffuseMap",
                "_Albedo",
                "_AlbedoMap",
                "_DetailMap",
            };

            foreach (var textureName in textureNames)
            {
                if (material.HasProperty(textureName))
                {
                    var texture = material.GetTexture(textureName);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }

            return null;
        }

        private void PlayLaunchSound(GameObject target, VehicleVariant variant)
        {
            if (target == null)
            {
                return;
            }

            if (variant.LaunchSound != null)
            {
                PlayAttachedClip(target, variant.LaunchSound, soundVolume);
                return;
            }

            var clip = CreateTrainClip(soundDuration);
            PlayAttachedClip(target, clip, soundVolume);
            Destroy(clip, soundDuration + 0.25f);
        }

        private void PlayAttachedClip(GameObject target, AudioClip clip, float volume)
        {
            if (target == null || clip == null)
            {
                return;
            }

            var source = target.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = soundSpatialBlend;
            source.minDistance = soundMinDistance;
            source.maxDistance = Mathf.Max(soundMinDistance + 0.01f, soundMaxDistance);
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
        }

        private void PlayConfiguredClip(AudioClip clip, Vector3 position, float volume)
        {
            PlayConfiguredClip(clip, position, volume, clip != null ? clip.length + 0.25f : 0f);
        }

        private void PlayConfiguredClip(AudioClip clip, Vector3 position, float volume, float maxDuration)
        {
            if (clip == null)
            {
                return;
            }

            var audioObject = new GameObject($"Railway Audio - {clip.name}");
            audioObject.transform.position = position;
            var source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = soundSpatialBlend;
            source.minDistance = soundMinDistance;
            source.maxDistance = Mathf.Max(soundMinDistance + 0.01f, soundMaxDistance);
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            Destroy(audioObject, Mathf.Max(0.01f, maxDuration));
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

        private sealed class VehicleVariantFallback : VehicleVariant
        {
            private readonly GameObject prefab;
            private readonly Vector3 localPosition;
            private readonly Vector3 localScale;
            private readonly Vector3 localEulerAngles;
            private readonly bool overrideSpawnRightOffset;
            private readonly float spawnRightOffset;
            private readonly AudioClip preLaunchSound;
            private readonly AudioClip launchSound;
            private readonly AudioClip hitSound;

            public VehicleVariantFallback(
                GameObject prefab,
                Vector3 localPosition,
                Vector3 localScale,
                Vector3 localEulerAngles,
                bool overrideSpawnRightOffset,
                float spawnRightOffset,
                AudioClip preLaunchSound,
                AudioClip launchSound,
                AudioClip hitSound)
            {
                this.prefab = prefab;
                this.localPosition = localPosition;
                this.localScale = localScale;
                this.localEulerAngles = localEulerAngles;
                this.overrideSpawnRightOffset = overrideSpawnRightOffset;
                this.spawnRightOffset = spawnRightOffset;
                this.preLaunchSound = preLaunchSound;
                this.launchSound = launchSound;
                this.hitSound = hitSound;
            }

            public override string Label => "Train";
            public override GameObject Prefab => prefab;
            public override Vector3 LocalPosition => localPosition;
            public override Vector3 LocalScale => localScale;
            public override Vector3 LocalEulerAngles => localEulerAngles;
            public override bool OverrideSpawnRightOffset => overrideSpawnRightOffset;
            public override float SpawnRightOffset => Mathf.Max(0f, spawnRightOffset);
            public override AudioClip PreLaunchSound => preLaunchSound;
            public override AudioClip LaunchSound => launchSound;
            public override AudioClip HitSound => hitSound;
            public override bool HasAnyContent => true;
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
        private AudioClip hitSound;
        private float hitSoundSpatialBlend;
        private float hitSoundMinDistance;
        private float hitSoundMaxDistance;
        private float hitSoundVolume;
        private GameObject hitEffectPrefab;
        private int hitEffectCount;
        private float hitEffectRadius;
        private float hitEffectScale;
        private float hitEffectLifetime;

        public void Initialize(
            string abilityId,
            GameObject owner,
            Vector3 travelDirection,
            float speed,
            float lifetime,
            float damage,
            float pushForce,
            Vector3 hitboxSize,
            LayerMask hitMask,
            AudioClip hitSound,
            float hitSoundSpatialBlend,
            float hitSoundMinDistance,
            float hitSoundMaxDistance,
            float hitSoundVolume,
            GameObject hitEffectPrefab,
            int hitEffectCount,
            float hitEffectRadius,
            float hitEffectScale,
            float hitEffectLifetime)
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
            this.hitSound = hitSound;
            this.hitSoundSpatialBlend = hitSoundSpatialBlend;
            this.hitSoundMinDistance = hitSoundMinDistance;
            this.hitSoundMaxDistance = hitSoundMaxDistance;
            this.hitSoundVolume = hitSoundVolume;
            this.hitEffectPrefab = hitEffectPrefab;
            this.hitEffectCount = Mathf.Max(0, hitEffectCount);
            this.hitEffectRadius = Mathf.Max(0f, hitEffectRadius);
            this.hitEffectScale = Mathf.Max(0f, hitEffectScale);
            this.hitEffectLifetime = Mathf.Max(0.1f, hitEffectLifetime);
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
            if (AbilityDamageUtility.TryApplyDamage(other, damageData))
            {
                SpawnHitEffects(hitPoint);

                if (hitSound != null)
                {
                    PlaySpatialClip(hitSound, hitPoint);
                    return;
                }

                PlayerCombatAudio.PlayRailwayPlayerHit(hitPoint, hitSoundVolume);
            }
        }

        private void PlaySpatialClip(AudioClip clip, Vector3 position)
        {
            var audioObject = new GameObject($"Railway Hit Audio - {clip.name}");
            audioObject.transform.position = position;
            var source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = hitSoundVolume;
            source.spatialBlend = hitSoundSpatialBlend;
            source.minDistance = hitSoundMinDistance;
            source.maxDistance = Mathf.Max(hitSoundMinDistance + 0.01f, hitSoundMaxDistance);
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            Destroy(audioObject, clip.length + 0.25f);
        }

        private void SpawnHitEffects(Vector3 hitPoint)
        {
            if (hitEffectPrefab == null || hitEffectCount <= 0 || hitEffectScale <= 0f)
            {
                return;
            }

            for (var i = 0; i < hitEffectCount; i++)
            {
                var offset2D = UnityEngine.Random.insideUnitCircle * hitEffectRadius;
                var position = hitPoint + new Vector3(offset2D.x, 0f, offset2D.y);
                var yaw = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                var effect = Instantiate(hitEffectPrefab, position, yaw);
                effect.transform.localScale *= hitEffectScale;
                Destroy(effect, hitEffectLifetime);
            }
        }
    }
}
