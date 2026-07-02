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
                recorder.StrokeCompleted += HandleStrokeCompleted;
            }
        }

        private void OnDisable()
        {
            if (recorder != null)
            {
                recorder.StrokeCompleted -= HandleStrokeCompleted;
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

        private void HandleStrokeCompleted(AirGestureStroke stroke)
        {
            var result = Recognize(stroke);
            if (!result.Succeeded)
            {
                if (logResults)
                {
                    Debug.Log("Air gesture rejected.");
                }

                GestureRejected?.Invoke(result);
                return;
            }

            if (Time.time < nextResultTime)
            {
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
                AddBuiltInTemplate("triangle", true, CreatePolylineTemplate(new[]
                {
                    new Vector2(0f, 0.58f),
                    new Vector2(-0.55f, -0.42f),
                    new Vector2(0.55f, -0.42f),
                    new Vector2(0f, 0.58f),
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
