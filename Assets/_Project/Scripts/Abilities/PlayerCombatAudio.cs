using UnityEngine;

namespace Wapawapa.Abilities
{
    public sealed class PlayerCombatAudio : MonoBehaviour
    {
        private const string LightPunchHitPath = "Audio/Abilities/LightPunchHit";
        private const string PunchSwingPath = "Audio/Abilities/PunchSwing";
        private const string BlackFlashHeavyPunchPath = "Audio/Abilities/BlackFlashHeavyPunch";
        private const string BlackFlashLightningPath = "Audio/Abilities/BlackFlashLightning";
        private const string RailwayPlayerHitPath = "Audio/Abilities/RailwayPlayerHit";

        [Header("Hit Sound Volumes")]
        [Min(0f)]
        [SerializeField] private float hitTakenVolume = 0.9f;
        [Min(0f)]
        [SerializeField] private float punchHitVolume = 0.75f;
        [Min(0f)]
        [SerializeField] private float blackFlashDefaultVolume = 1f;
        [Min(0f)]
        [SerializeField] private float railwayPlayerHitDefaultVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float railwayPlayerHitSpatialBlend = 0.15f;
        [Min(0f)]
        [SerializeField] private float railwayPlayerHitMinDistance = 8f;
        [Min(0.01f)]
        [SerializeField] private float railwayPlayerHitMaxDistance = 80f;

        private static PlayerCombatAudio activeSettings;
        private static AudioClip hitTakenClip;
        private static AudioClip hitLandedClip;
        private static AudioClip punchSwingClip;
        private static AudioClip blackFlashHeavyPunchClip;
        private static AudioClip blackFlashLightningClip;
        private static AudioClip railwayPlayerHitClip;
        private static float nextPunchSwingTime;

        private void OnEnable()
        {
            activeSettings = this;
        }

        private void OnDisable()
        {
            if (activeSettings == this)
            {
                activeSettings = null;
            }
        }

        public static void PlayLocalHitTaken(Vector3 position)
        {
            EnsureClips();
            PlayClip(hitTakenClip, position, GetVolume(settings => settings.hitTakenVolume, 0.9f));
        }

        public static void PlayLocalHitLanded(Vector3 position)
        {
            EnsureClips();
            PlayClip(hitLandedClip, position, GetVolume(settings => settings.punchHitVolume, 0.75f));
        }

        public static void PlayPunchSwing(Vector3 position, float volume = 0.8f)
        {
            if (Time.time < nextPunchSwingTime)
            {
                return;
            }

            EnsureClips();
            nextPunchSwingTime = Time.time + 0.08f;
            PlayClip(punchSwingClip, position, volume);
        }

        public static void PlayBlackFlashImpact(Vector3 position, AudioClip heavyPunchOverride = null, AudioClip lightningOverride = null, float volume = 1f)
        {
            EnsureClips();
            PlayClip(heavyPunchOverride != null ? heavyPunchOverride : blackFlashHeavyPunchClip, position, volume);
            PlayClip(lightningOverride != null ? lightningOverride : blackFlashLightningClip, position, volume);
        }

        public static void PlayBlackFlashImpact(Vector3 position)
        {
            PlayBlackFlashImpact(position, null, null, GetVolume(settings => settings.blackFlashDefaultVolume, 1f));
        }

        public static void PlayRailwayPlayerHit(Vector3 position, float volume = -1f)
        {
            EnsureClips();
            var resolvedVolume = volume >= 0f
                ? volume
                : GetVolume(settings => settings.railwayPlayerHitDefaultVolume, 1f);
            PlaySpatialClip(
                railwayPlayerHitClip,
                position,
                resolvedVolume,
                GetVolume(settings => settings.railwayPlayerHitSpatialBlend, 0.15f),
                GetVolume(settings => settings.railwayPlayerHitMinDistance, 8f),
                GetVolume(settings => settings.railwayPlayerHitMaxDistance, 80f));
        }

        private static void EnsureClips()
        {
            hitTakenClip ??= CreateHitTakenClip();
            hitLandedClip ??= LoadClip(LightPunchHitPath) ?? CreateHitLandedClip();
            punchSwingClip ??= LoadClip(PunchSwingPath);
            blackFlashHeavyPunchClip ??= LoadClip(BlackFlashHeavyPunchPath) ?? CreateBlackFlashFallbackClip();
            blackFlashLightningClip ??= LoadClip(BlackFlashLightningPath);
            railwayPlayerHitClip ??= LoadClip(RailwayPlayerHitPath);
        }

        private static AudioClip LoadClip(string path)
        {
            return Resources.Load<AudioClip>(path);
        }

        private static void PlayClip(AudioClip clip, Vector3 position, float volume)
        {
            if (clip == null)
            {
                return;
            }

            AudioSource.PlayClipAtPoint(clip, position, volume);
        }

        private static void PlaySpatialClip(AudioClip clip, Vector3 position, float volume, float spatialBlend, float minDistance, float maxDistance)
        {
            if (clip == null)
            {
                return;
            }

            var audioObject = new GameObject($"Combat Audio - {clip.name}");
            audioObject.transform.position = position;
            var source = audioObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = spatialBlend;
            source.minDistance = minDistance;
            source.maxDistance = Mathf.Max(minDistance + 0.01f, maxDistance);
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            Object.Destroy(audioObject, clip.length + 0.25f);
        }

        private static float GetVolume(System.Func<PlayerCombatAudio, float> selector, float fallback)
        {
            return activeSettings != null ? selector(activeSettings) : fallback;
        }

        private static AudioClip CreateHitTakenClip()
        {
            return CreateClip("HitTaken", 0.18f, sample =>
            {
                var thump = Mathf.Sin(2f * Mathf.PI * 92f * sample.Time) * 0.7f;
                var crack = Mathf.Sin(2f * Mathf.PI * 230f * sample.Time) * 0.25f;
                return (thump + crack) * sample.Decay;
            });
        }

        private static AudioClip CreateHitLandedClip()
        {
            return CreateClip("HitLanded", 0.16f, sample =>
            {
                var click = Mathf.Sin(2f * Mathf.PI * 520f * sample.Time) * 0.35f;
                var punch = Mathf.Sin(2f * Mathf.PI * 145f * sample.Time) * 0.55f;
                return (click + punch) * sample.Decay;
            });
        }

        private static AudioClip CreateBlackFlashFallbackClip()
        {
            return CreateClip("BlackFlashFallback", 0.28f, sample =>
            {
                var lowHit = Mathf.Sin(2f * Mathf.PI * 72f * sample.Time) * 0.75f;
                var highCrack = Mathf.Sin(2f * Mathf.PI * 1480f * sample.Time) * 0.22f;
                var grit = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 620f * sample.Time)) * 0.18f;
                return lowHit + highCrack + grit;
            });
        }

        private static AudioClip CreateClip(string name, float duration, System.Func<Sample, float> sampler)
        {
            const int sampleRate = 22050;
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var time = i / (float)sampleRate;
                var decay = Mathf.Exp(-time * 18f);
                data[i] = Mathf.Clamp(sampler(new Sample(time, decay)), -1f, 1f);
            }

            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private readonly struct Sample
        {
            public Sample(float time, float decay)
            {
                Time = time;
                Decay = decay;
            }

            public float Time { get; }
            public float Decay { get; }
        }
    }
}
