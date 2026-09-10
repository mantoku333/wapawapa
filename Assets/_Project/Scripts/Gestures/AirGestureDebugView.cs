using UnityEngine;

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
        private ParticleSystem sparkles;
        private Mesh sparkleMesh;
        private int sparkledPointCount;

        private void Awake()
        {
            var trail = new GameObject("Air Gesture Trail");
            trail.transform.SetParent(transform, false);
            lineRenderer = trail.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.sharedMaterial = trailMaterial;
            lineRenderer.widthMultiplier = trailWidth;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.positionCount = 0;
            CreateSparkles();
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
            if (recognizer != null)
            {
                recognizer.GestureRecognized += HandleGestureRecognized;
                recognizer.GestureRejected += HandleGestureRejected;
            }
        }

        private void OnDisable()
        {
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
            if (sparkles != null)
            {
                Destroy(sparkles.gameObject);
            }
            if (sparkleMesh != null)
            {
                Destroy(sparkleMesh);
            }
            if (lineRenderer != null)
            {
                Destroy(lineRenderer.gameObject);
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
