using UnityEngine;

namespace Wapawapa.Gestures
{
    [CreateAssetMenu(menuName = "Wapawapa/Gestures/Air Gesture Template")]
    public sealed class AirGestureTemplate : ScriptableObject
    {
        [SerializeField] private string gestureId = "circle";
        [SerializeField] private bool closedShape = true;
        [SerializeField] private Vector2[] normalizedPoints = System.Array.Empty<Vector2>();

        public string GestureId => gestureId;
        public bool ClosedShape => closedShape;
        public Vector2[] NormalizedPoints => normalizedPoints;
    }
}
