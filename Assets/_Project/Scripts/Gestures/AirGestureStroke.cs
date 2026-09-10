using UnityEngine;

namespace Wapawapa.Gestures
{
    public readonly struct AirGestureStroke
    {
        public AirGestureStroke(
            Vector3[] worldPoints,
            float duration,
            Vector3 planeOrigin,
            Vector3 planeRight,
            Vector3 planeUp)
        {
            WorldPoints = worldPoints;
            Duration = duration;
            PlaneOrigin = planeOrigin;
            PlaneRight = planeRight.normalized;
            PlaneUp = planeUp.normalized;
        }

        public Vector3[] WorldPoints { get; }
        public float Duration { get; }
        public Vector3 PlaneOrigin { get; }
        public Vector3 PlaneRight { get; }
        public Vector3 PlaneUp { get; }
        public bool IsValid => WorldPoints != null && WorldPoints.Length > 1;
    }
}
