using UnityEngine;

namespace Wapawapa.Gestures
{
    public static class AirGestureProjector
    {
        public static Vector2[] ProjectToStrokePlane(in AirGestureStroke stroke)
        {
            if (!stroke.IsValid)
            {
                return System.Array.Empty<Vector2>();
            }

            var projected = new Vector2[stroke.WorldPoints.Length];
            for (var i = 0; i < stroke.WorldPoints.Length; i++)
            {
                var delta = stroke.WorldPoints[i] - stroke.PlaneOrigin;
                projected[i] = new Vector2(
                    Vector3.Dot(delta, stroke.PlaneRight),
                    Vector3.Dot(delta, stroke.PlaneUp));
            }

            return projected;
        }
    }
}
