using UnityEngine;

namespace Wapawapa.Gestures
{
    [CreateAssetMenu(menuName = "Wapawapa/Gestures/Air Gesture Template Set")]
    public sealed class AirGestureTemplateSet : ScriptableObject
    {
        [SerializeField] private AirGestureTemplate[] templates = System.Array.Empty<AirGestureTemplate>();

        public AirGestureTemplate[] Templates => templates;
    }
}
