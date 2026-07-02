using UnityEngine;

namespace Wapawapa.Gestures
{
    [RequireComponent(typeof(LineRenderer))]
    public sealed class AirGestureDebugView : MonoBehaviour
    {
        [SerializeField] private AirGestureRecorder recorder;
        [SerializeField] private AirGestureRecognizer recognizer;
        [SerializeField] private bool logRecognition = true;

        private LineRenderer lineRenderer;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
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
                lineRenderer.positionCount = 0;
                return;
            }

            var points = recorder.CurrentPoints;
            lineRenderer.positionCount = points.Count;
            for (var i = 0; i < points.Count; i++)
            {
                lineRenderer.SetPosition(i, points[i]);
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
