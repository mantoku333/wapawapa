using UnityEngine;

namespace Wapawapa.Gameplay
{
    public sealed class XinZhaoBattleRoyaleSettings : ScriptableObject
    {
        [SerializeField] private AudioClip impactSound;

        public AudioClip ImpactSound => impactSound;
    }
}
