using UnityEngine;
using System.Collections.Generic;

namespace Wapawapa.Gestures
{
    public sealed class AirGestureDebugView : MonoBehaviour
    {
        [SerializeField] private AirGestureRecorder recorder;
        [SerializeField] private AirGestureRecognizer recognizer;
        [SerializeField] private bool logRecognition = true;
        [SerializeField] private Material trailMaterial;
        [SerializeField, Min(0.001f)] private float trailWidth = 0.012f;
        [SerializeField] private bool showSparkles = true;
        [SerializeField, Min(0.001f)] private float sparkleSize = 0.009f;

        private LineRenderer lineRenderer;
        private readonly List<LineRenderer> completedLines = new List<LineRenderer>();
        private MaterialPropertyBlock lineProperties;
        private ParticleSystem sparkles;
        private Mesh sparkleMesh;
        private int sparkledPointCount;

        private void Awake()
        {
            EnsureResources();
        }

        private void EnsureResources()
        {
            // Native Unity resources must be created on the main thread, not in field initializers.
            // OnEnable also restores the non-serialized property block after a script reload.
            if (lineProperties == null) lineProperties = new MaterialPropertyBlock();
            if (lineRenderer == null) lineRenderer = CreateLine();
            if (sparkles == null) CreateSparkles();
        }

        private LineRenderer CreateLine()
        {
            var trail = new GameObject("Air Gesture Trail");
            trail.transform.SetParent(transform, false);
            var line = trail.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = trailMaterial;
            line.widthMultiplier = trailWidth;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = 0;
            return line;
        }

        private void CreateSparkles()
        {
            var sparkleObject = new GameObject("Air Gesture Sparkles");
            sparkleObject.transform.SetParent(transform, false);
            sparkles = sparkleObject.AddComponent<ParticleSystem>();
            sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = sparkles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            main.startSize = new ParticleSystem.MinMaxCurve(sparkleSize * 0.5f, sparkleSize);
            main.startSpeed = 0f;
            main.gravityModifier = 0.015f;
            main.maxParticles = 256;
            var emission = sparkles.emission;
            emission.enabled = false;
            var shape = sparkles.shape;
            shape.enabled = false;

            // Small faceted glints shrink and flash without needing bloom or a texture.
            sparkleMesh = new Mesh { name = "Air Gesture Glint" };
            sparkleMesh.vertices = new[]
            {
                Vector3.up, Vector3.down, Vector3.left * 0.45f,
                Vector3.right * 0.45f, Vector3.forward * 0.45f, Vector3.back * 0.45f,
            };
            sparkleMesh.triangles = new[]
            {
                0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4,
                1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2,
            };
            sparkleMesh.RecalculateBounds();
            var renderer = sparkleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = sparkleMesh;
            renderer.sharedMaterial = trailMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var size = sparkles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.2f), new Keyframe(0.15f, 1f),
                new Keyframe(0.4f, 0.3f), new Keyframe(0.6f, 0.75f),
                new Keyframe(1f, 0f)));
            var rotation = sparkles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
        }

        private void OnEnable()
        {
            EnsureResources();
            if (recorder != null) recorder.SignResolved += HandleSignResolved;
            if (recognizer != null)
            {
                recognizer.GestureRecognized += HandleGestureRecognized;
                recognizer.GestureRejected += HandleGestureRejected;
            }
        }

        private void OnDisable()
        {
            if (recorder != null) recorder.SignResolved -= HandleSignResolved;
            foreach (var line in completedLines) line.positionCount = 0;
            sparkledPointCount = 0;
            if (sparkles != null)
            {
                sparkles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
            }

            if (recognizer != null)
            {
                recognizer.GestureRecognized -= HandleGestureRecognized;
                recognizer.GestureRejected -= HandleGestureRejected;
            }
        }

        private void LateUpdate()
        {
            var count = recorder != null ? recorder.Strokes.Count : 0;
            while (completedLines.Count < count) completedLines.Add(CreateLine());
            var color = recorder != null && recorder.ShowingFeedback ?
                (recorder.LastSignAccepted ? new Color(0.2f, 1f, 0.45f) : new Color(1f, 0.15f, 0.1f)) :
                new Color(0.1f, 0.9f, 1f);
            lineProperties.SetColor("_BaseColor", color);
            for (var i = 0; i < completedLines.Count; i++)
            {
                var line = completedLines[i];
                line.SetPropertyBlock(lineProperties);
                if (i >= count) { line.positionCount = 0; continue; }
                var strokePoints = recorder.Strokes[i].WorldPoints;
                line.positionCount = strokePoints.Length;
                line.SetPositions(strokePoints);
            }
            if (recorder == null || !recorder.IsRecording)
            {
                sparkledPointCount = 0;
                lineRenderer.positionCount = 0;
                return;
            }

            var points = recorder.CurrentPoints;
            lineRenderer.positionCount = points.Count;
            for (var i = 0; i < points.Count; i++)
            {
                lineRenderer.SetPosition(i, points[i]);
            }

            if (showSparkles)
            {
                if (!sparkles.isPlaying)
                {
                    sparkles.Play();
                }

                // Emit only along newly sampled motion, with a bounded catch-up after a slow frame.
                for (var i = Mathf.Max(sparkledPointCount, points.Count - 24); i < points.Count; i++)
                {
                    var particle = new ParticleSystem.EmitParams
                    {
                        position = points[i] + Random.insideUnitSphere * 0.006f,
                        velocity = Random.insideUnitSphere * 0.045f,
                        rotation = Random.Range(0f, 360f),
                    };
                    sparkles.Emit(particle, 2);
                }
            }
            sparkledPointCount = points.Count;
        }

        private void OnDestroy()
        {
            foreach (var line in completedLines)
                if (line != null) ReleaseResource(line.gameObject);
            if (sparkles != null)
            {
                ReleaseResource(sparkles.gameObject);
            }
            if (sparkleMesh != null)
            {
                ReleaseResource(sparkleMesh);
            }
            if (lineRenderer != null)
            {
                ReleaseResource(lineRenderer.gameObject);
            }
        }

        private static void ReleaseResource(Object resource)
        {
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }

        private void HandleSignResolved(bool accepted)
        {
            if (!accepted || !showSparkles || recorder == null) return;
            if (!sparkles.isPlaying) sparkles.Play();
            foreach (var stroke in recorder.Strokes)
            {
                var step = Mathf.Max(1, stroke.WorldPoints.Length / 24);
                for (var i = 0; i < stroke.WorldPoints.Length; i += step)
                {
                    var particle = new ParticleSystem.EmitParams
                    {
                        position = stroke.WorldPoints[i],
                        velocity = Random.insideUnitSphere * 0.35f,
                    };
                    sparkles.Emit(particle, 2);
                }
            }
        }

        private void HandleGestureRecognized(AirGestureResult result)
        {
            if (logRecognition)
            {
                Debug.Log($"Gesture debug: recognized {result.GestureId} ({result.Confidence:0.00}).");
            }
        }

        private void HandleGestureRejected(AirGestureResult result)
        {
            if (logRecognition)
            {
                Debug.Log("Gesture debug: rejected stroke.");
            }
        }
    }
}
