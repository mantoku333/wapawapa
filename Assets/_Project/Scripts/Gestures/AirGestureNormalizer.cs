using System.Collections.Generic;
using UnityEngine;

namespace Wapawapa.Gestures
{
    public static class AirGestureNormalizer
    {
        private const float DuplicatePointDistance = 0.0001f;

        public static bool TryNormalize(
            IReadOnlyList<Vector2> points,
            int targetPointCount,
            float minimumPathLength,
            float minimumBoundsSize,
            out Vector2[] normalized,
            out float pathLength,
            out Vector2 boundsSize)
        {
            normalized = System.Array.Empty<Vector2>();
            pathLength = 0f;
            boundsSize = Vector2.zero;

            if (points == null || points.Count < 2 || targetPointCount < 2)
            {
                return false;
            }

            var clean = RemoveDuplicatePoints(points);
            if (clean.Count < 2)
            {
                return false;
            }

            pathLength = CalculatePathLength(clean);
            boundsSize = CalculateBoundsSize(clean);
            if (pathLength < minimumPathLength || Mathf.Max(boundsSize.x, boundsSize.y) < minimumBoundsSize)
            {
                return false;
            }

            var sampled = Resample(clean, targetPointCount, pathLength);
            var centroid = CalculateCentroid(sampled);
            var sampledBounds = CalculateBoundsSize(sampled);
            var scale = Mathf.Max(sampledBounds.x, sampledBounds.y);
            if (scale <= Mathf.Epsilon)
            {
                return false;
            }

            normalized = new Vector2[sampled.Length];
            for (var i = 0; i < sampled.Length; i++)
            {
                normalized[i] = (sampled[i] - centroid) / scale;
            }

            return true;
        }

        public static float CalculatePathLength(IReadOnlyList<Vector2> points)
        {
            var length = 0f;
            for (var i = 1; i < points.Count; i++)
            {
                length += Vector2.Distance(points[i - 1], points[i]);
            }

            return length;
        }

        private static List<Vector2> RemoveDuplicatePoints(IReadOnlyList<Vector2> points)
        {
            var clean = new List<Vector2>(points.Count) { points[0] };
            for (var i = 1; i < points.Count; i++)
            {
                if ((points[i] - clean[clean.Count - 1]).sqrMagnitude >= DuplicatePointDistance * DuplicatePointDistance)
                {
                    clean.Add(points[i]);
                }
            }

            return clean;
        }

        private static Vector2[] Resample(IReadOnlyList<Vector2> points, int targetPointCount, float pathLength)
        {
            var sampled = new List<Vector2>(targetPointCount) { points[0] };
            var interval = pathLength / (targetPointCount - 1);
            var distanceSinceLastSample = 0f;
            var previous = points[0];
            var index = 1;

            while (index < points.Count && sampled.Count < targetPointCount)
            {
                var current = points[index];
                var segmentLength = Vector2.Distance(previous, current);
                if (segmentLength <= Mathf.Epsilon)
                {
                    previous = current;
                    index++;
                    continue;
                }

                if (distanceSinceLastSample + segmentLength >= interval)
                {
                    var t = (interval - distanceSinceLastSample) / segmentLength;
                    var sample = Vector2.Lerp(previous, current, t);
                    sampled.Add(sample);
                    previous = sample;
                    distanceSinceLastSample = 0f;
                }
                else
                {
                    distanceSinceLastSample += segmentLength;
                    previous = current;
                    index++;
                }
            }

            while (sampled.Count < targetPointCount)
            {
                sampled.Add(points[points.Count - 1]);
            }

            return sampled.ToArray();
        }

        private static Vector2 CalculateCentroid(IReadOnlyList<Vector2> points)
        {
            var sum = Vector2.zero;
            for (var i = 0; i < points.Count; i++)
            {
                sum += points[i];
            }

            return sum / points.Count;
        }

        private static Vector2 CalculateBoundsSize(IReadOnlyList<Vector2> points)
        {
            var min = points[0];
            var max = points[0];
            for (var i = 1; i < points.Count; i++)
            {
                min = Vector2.Min(min, points[i]);
                max = Vector2.Max(max, points[i]);
            }

            return max - min;
        }
    }
}
