using UnityEngine;

namespace Wapawapa.Abilities
{
    public sealed class PlayerCombatAudio : MonoBehaviour
    {
        private static AudioClip hitTakenClip;
        private static AudioClip hitLandedClip;

        public static void PlayLocalHitTaken(Vector3 position)
        {
            EnsureClips();
            AudioSource.PlayClipAtPoint(hitTakenClip, position, 0.9f);
        }

        public static void PlayLocalHitLanded(Vector3 position)
        {
            EnsureClips();
            AudioSource.PlayClipAtPoint(hitLandedClip, position, 0.75f);
        }

        private static void EnsureClips()
        {
            hitTakenClip ??= CreateHitTakenClip();
            hitLandedClip ??= CreateHitLandedClip();
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
