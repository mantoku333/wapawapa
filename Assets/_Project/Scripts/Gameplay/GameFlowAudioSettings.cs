using UnityEngine;

namespace Wapawapa.Gameplay
{
    public sealed class GameFlowAudioSettings : ScriptableObject
    {
        [SerializeField] private AudioClip gameStartVoice;
        [SerializeField] private AudioClip gameStartMusic;
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] private AudioClip resultSting;

        public AudioClip GameStartVoice => gameStartVoice;
        public AudioClip GameStartMusic => gameStartMusic;
        public AudioClip BackgroundMusic => backgroundMusic;
        public AudioClip ResultSting => resultSting;
    }
}
