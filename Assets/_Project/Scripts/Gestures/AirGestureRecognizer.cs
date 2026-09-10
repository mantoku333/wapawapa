using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wapawapa.Gestures
{
    public sealed class AirGestureRecognizer : MonoBehaviour
    {
        private readonly struct RuntimeTemplate
        {
            public RuntimeTemplate(string gestureId, bool closedShape, Vector2[] points)
            {
                GestureId = gestureId;
                ClosedShape = closedShape;
                Points = points;
            }

            public string GestureId { get; }
            public bool ClosedShape { get; }
            public Vector2[] Points { get; }
        }

        [Header("Input")]
        [SerializeField] private AirGestureRecorder recorder;
        [SerializeField] private AirGestureTemplateSet templateSet;
        [SerializeField] private bool includeBuiltInTemplates = true;

        [Header("Normalization")]
        [SerializeField] private int resamplePointCount = 64;
        [SerializeField] private float minimumPathLength = 0.18f;
        [SerializeField] private float minimumBoundsSize = 0.08f;
        [SerializeField] private float minimumDuration = 0.12f;
        [SerializeField] private float maximumDuration = 2.5f;

        [Header("Matching")]
        [SerializeField] private float maximumScore = 0.22f;
        [SerializeField] private float minimumConfidence = 0.35f;
        [SerializeField] private int closedShapeShiftStep = 4;
        [SerializeField] private float rotationSearchDegrees = 30f;
        [SerializeField] private float rotationSearchStepDegrees = 15f;
        [SerializeField] private float resultCooldown = 0.25f;
        [SerializeField] private bool logResults;

        private readonly List<RuntimeTemplate> templates = new List<RuntimeTemplate>();
        private float nextResultTime;

        public event Action<AirGestureResult> GestureRecognized;
        public event Action<AirGestureResult> GestureRejected;

        private void Awake()
        {
            RebuildTemplates();
        }

        private void OnEnable()
        {
            if (recorder != null)
            {
                recorder.SignSubmitted += HandleSignSubmitted;
            }
        }

        private void OnDisable()
        {
            if (recorder != null)
            {
                recorder.SignSubmitted -= HandleSignSubmitted;
            }
        }

        public AirGestureResult Recognize(in AirGestureStroke stroke)
        {
            if (!stroke.IsValid || stroke.Duration < minimumDuration || stroke.Duration > maximumDuration)
            {
                return AirGestureResult.Failed(stroke.WorldPoints?.Length ?? 0);
            }

            var projected = AirGestureProjector.ProjectToStrokePlane(stroke);
            if (!AirGestureNormalizer.TryNormalize(
                    projected,
                    resamplePointCount,
                    minimumPathLength,
                    minimumBoundsSize,
                    out var normalized,
                    out _,
                    out _))
            {
                return AirGestureResult.Failed(projected.Length);
            }

            return RecognizeNormalized(normalized);
        }

        private AirGestureResult RecognizeNormalized(Vector2[] normalized)
        {
            if (templates.Count == 0)
            {
                RebuildTemplates();
            }

            var bestGestureId = string.Empty;
            var bestScore = float.PositiveInfinity;
            for (var i = 0; i < templates.Count; i++)
            {
                var template = templates[i];
                if (template.Points == null || template.Points.Length != normalized.Length)
                {
                    continue;
                }

                var score = CalculateBestScore(normalized, template);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestGestureId = template.GestureId;
                }
            }

            var confidence = Mathf.Clamp01(1f - bestScore / maximumScore);
            if (string.IsNullOrEmpty(bestGestureId) || bestScore > maximumScore || confidence < minimumConfidence)
            {
                return AirGestureResult.Failed(normalized.Length);
            }

            return new AirGestureResult(bestGestureId, confidence, bestScore, normalized.Length);
        }

        public AirGestureResult RecognizeSign(IReadOnlyList<AirGestureStroke> strokes)
        {
            if (strokes == null || strokes.Count == 0 || strokes.Count > 2)
                return AirGestureResult.Failed();
            if (strokes.Count == 1) return Recognize(strokes[0]);

            // Both strokes are projected in the first stroke's plane before checking layout.
            var first = strokes[0];
            var second = new AirGestureStroke(strokes[1].WorldPoints, strokes[1].Duration,
                first.PlaneOrigin, first.PlaneRight, first.PlaneUp);
            var body = AirGestureProjector.ProjectToStrokePlane(first);
            var mark = AirGestureProjector.ProjectToStrokePlane(second);
            if (!first.IsValid || !second.IsValid || first.Duration < minimumDuration ||
                second.Duration < 0.06f || first.Duration > 5f || second.Duration > 5f)
                return AirGestureResult.Failed();

            var bodyBounds = GetBounds(body);
            var markBounds = GetBounds(mark);
            var width = bodyBounds.size.x;
            var markSize = Mathf.Max(markBounds.size.x, markBounds.size.y);
            if (width < minimumBoundsSize || markSize < width * 0.1f || markSize > width * 0.6f ||
                markBounds.center.x < bodyBounds.min.x + width * 0.45f ||
                markBounds.center.x > bodyBounds.max.x + width * 0.55f ||
                markBounds.center.y < bodyBounds.max.y - width * 0.12f ||
                markBounds.center.y > bodyBounds.max.y + width * 0.7f)
                return AirGestureResult.Failed();

            var normal = Vector3.Cross(first.PlaneRight, first.PlaneUp).normalized;
            foreach (var point in second.WorldPoints)
                if (Mathf.Abs(Vector3.Dot(point - first.PlaneOrigin, normal)) > Mathf.Max(0.18f, width * 0.5f))
                    return AirGestureResult.Failed();

            var bodyResult = RecognizePart(body, "he", 0.08f);
            var markResult = RecognizePart(mark, "circle", 0.015f);
            if (!bodyResult.Succeeded || !markResult.Succeeded) return AirGestureResult.Failed();
            return new AirGestureResult("pe", Mathf.Min(bodyResult.Confidence, markResult.Confidence),
                Mathf.Max(bodyResult.Score, markResult.Score), body.Length + mark.Length);
        }

        private static Bounds GetBounds(Vector2[] points)
        {
            if (points.Length == 0) return new Bounds();
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(point);
            return bounds;
        }

        private AirGestureResult RecognizePart(Vector2[] points, string id, float minimumSize)
        {
            if (!AirGestureNormalizer.TryNormalize(points, resamplePointCount, minimumSize * 2f,
                minimumSize, out var normalized, out _, out var size)) return AirGestureResult.Failed();
            if (id == "circle" && Vector2.Distance(points[0], points[points.Length - 1]) >
                Mathf.Max(size.x, size.y) * 0.45f) return AirGestureResult.Failed();
            if (templates.Count == 0) RebuildTemplates();
            foreach (var template in templates)
            {
                if (template.GestureId != id) continue;
                var score = CalculateBestScore(normalized, template);
                var confidence = Mathf.Clamp01(1f - score / maximumScore);
                return confidence >= minimumConfidence ?
                    new AirGestureResult(id, confidence, score, points.Length) : AirGestureResult.Failed();
            }
            return AirGestureResult.Failed();
        }

        public void ConfirmActivation(bool accepted)
        {
            if (recorder != null) recorder.ResolveSign(accepted);
        }

        private void HandleSignSubmitted(IReadOnlyList<AirGestureStroke> strokes)
        {
            var result = RecognizeSign(strokes);
            if (!result.Succeeded)
            {
                if (logResults)
                {
                    Debug.Log("Air sign rejected on punch. Draw again after the red flash.");
                }

                GestureRejected?.Invoke(result);
                ConfirmActivation(false);
                return;
            }

            if (Time.time < nextResultTime)
            {
                ConfirmActivation(false);
                return;
            }

            nextResultTime = Time.time + resultCooldown;
            if (logResults)
            {
                Debug.Log($"Air gesture recognized: {result.GestureId} confidence={result.Confidence:0.00} score={result.Score:0.000}");
            }

            GestureRecognized?.Invoke(result);
        }

        private void RebuildTemplates()
        {
            templates.Clear();

            if (includeBuiltInTemplates)
            {
                AddBuiltInTemplate("circle", true, CreateCirclePoints(resamplePointCount));
                AddBuiltInTemplate("he", false, CreatePolylineTemplate(new[]
                {
                    new Vector2(-0.5f, -0.05f), new Vector2(-0.18f, 0.3f), new Vector2(0.5f, -0.25f),
                }));
                AddBuiltInTemplate("triangle", true, CreatePolylineTemplate(new[]
                {
                    new Vector2(0f, 0.58f),
                    new Vector2(-0.55f, -0.42f),
                    new Vector2(0.55f, -0.42f),
                    new Vector2(0f, 0.58f),
                }));
                AddBuiltInTemplate("square", true, CreatePolylineTemplate(new[]
                {
                    new Vector2(-0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, -0.5f),
                    new Vector2(-0.5f, -0.5f),
                    new Vector2(-0.5f, 0.5f),
                }));
            }

            if (templateSet == null)
            {
                return;
            }

            var assetTemplates = templateSet.Templates;
            for (var i = 0; i < assetTemplates.Length; i++)
            {
                var template = assetTemplates[i];
                if (template == null || string.IsNullOrWhiteSpace(template.GestureId))
                {
                    continue;
                }

                var points = template.NormalizedPoints;
                if (points == null || points.Length < 2)
                {
                    continue;
                }

                if (AirGestureNormalizer.TryNormalize(
                        points,
                        resamplePointCount,
                        0f,
                        0f,
                        out var normalized,
                        out _,
                        out _))
                {
                    templates.Add(new RuntimeTemplate(template.GestureId, template.ClosedShape, normalized));
                }
            }
        }

        private void AddBuiltInTemplate(string gestureId, bool closedShape, Vector2[] points)
        {
            templates.Add(new RuntimeTemplate(gestureId, closedShape, points));
        }

        private Vector2[] CreatePolylineTemplate(Vector2[] points)
        {
            AirGestureNormalizer.TryNormalize(
                points,
                resamplePointCount,
                0f,
                0f,
                out var normalized,
                out _,
                out _);
            return normalized;
        }

        private Vector2[] CreateCirclePoints(int count)
        {
            var points = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                var radians = Mathf.PI * 2f * i / (count - 1);
                points[i] = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 0.5f;
            }

            return points;
        }

        private float CalculateBestScore(Vector2[] candidate, RuntimeTemplate template)
        {
            var best = float.PositiveInfinity;
            var shiftStep = template.ClosedShape ? Mathf.Max(1, closedShapeShiftStep) : candidate.Length;

            for (var direction = 0; direction < 2; direction++)
            {
                for (var shift = 0; shift < candidate.Length; shift += shiftStep)
                {
                    for (var angle = -rotationSearchDegrees; angle <= rotationSearchDegrees + 0.001f; angle += rotationSearchStepDegrees)
                    {
                        var score = CalculateScore(candidate, template.Points, template.ClosedShape ? shift : 0, direction == 1, angle);
                        if (score < best)
                        {
                            best = score;
                        }
                    }
                }
            }

            return best;
        }

        private static float CalculateScore(Vector2[] candidate, Vector2[] template, int shift, bool reversed, float rotationDegrees)
        {
            var radians = rotationDegrees * Mathf.Deg2Rad;
            var sin = Mathf.Sin(radians);
            var cos = Mathf.Cos(radians);
            var total = 0f;

            for (var i = 0; i < candidate.Length; i++)
            {
                var candidateIndex = reversed
                    ? (candidate.Length - 1 - ((i + shift) % candidate.Length))
                    : (i + shift) % candidate.Length;
                var point = candidate[candidateIndex];
                var rotated = new Vector2(
                    point.x * cos - point.y * sin,
                    point.x * sin + point.y * cos);
                total += Vector2.Distance(rotated, template[i]);
            }

            return total / candidate.Length;
        }
    }
}
