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
        [SerializeField] private bool ignoreHandToHandHits = true;
        [SerializeField] private bool ignoreSelfHits = true;

        public string PunchId => punchId;
        public float Damage => damage;
        public float MinimumHitSpeed => minimumHitSpeed;
        public float PushForce => pushForce;
        public float RepeatHitDelay => repeatHitDelay;
        public bool IgnoreHandToHandHits => ignoreHandToHandHits;
        public bool IgnoreSelfHits => ignoreSelfHits;
    }
}
