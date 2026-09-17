using UnityEngine;

namespace Wapawapa.Boxing
{
    public sealed class PlayerPunchSettings : MonoBehaviour
    {
        [SerializeField] private string punchId = "basic.punch";
        [SerializeField] private float damage = 10f;
        [SerializeField] private float minimumHitSpeed = 1.4f;
        [SerializeField] private float pushForce = 4f;
        [SerializeField] private float repeatHitDelay = 0.25f;
        [Min(0f)]
        [SerializeField] private float punchSwingVolume = 0.8f;
        [SerializeField] private bool ignoreHandToHandHits = true;
        [SerializeField] private bool ignoreSelfHits = true;

        [Min(0.01f)] [SerializeField] private float minimumPunchDistance = 0.08f;
        [Range(0f, 1f)] [SerializeField] private float minimumForwardDot = 0.65f;
        [Min(0.01f)] [SerializeField] private float punchRetractDistance = 0.08f;
        [Range(0f, 1f)] [SerializeField] private float hitHapticAmplitude = 0.5f;
        [Min(0f)] [SerializeField] private float hitHapticDuration = 0.08f;

        public float MinimumPunchDistance => Mathf.Max(0.01f, minimumPunchDistance);
        public float MinimumForwardDot => Mathf.Clamp01(minimumForwardDot);
        public float PunchRetractDistance => Mathf.Max(0.01f, punchRetractDistance);
        public float HitHapticAmplitude => Mathf.Clamp01(hitHapticAmplitude);
        public float HitHapticDuration => Mathf.Max(0f, hitHapticDuration);

        public string PunchId => punchId;
        public float Damage => damage;
        public float MinimumHitSpeed => minimumHitSpeed;
        public float PushForce => pushForce;
        public float RepeatHitDelay => repeatHitDelay;
        public float PunchSwingVolume => punchSwingVolume;
        public bool IgnoreHandToHandHits => ignoreHandToHandHits;
        public bool IgnoreSelfHits => ignoreSelfHits;
    }
}
