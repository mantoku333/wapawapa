using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace Wapawapa.Gestures
{
    public sealed class AirGestureRecorder : MonoBehaviour
    {
        [Header("Tracking")]
        [SerializeField] private Transform drawPoint;
        [SerializeField] private Transform viewReference;
        [SerializeField] private XRNode inputHand = XRNode.RightHand;

        [Header("Draw Gate")]
        [SerializeField] private bool requireTrigger = true;
        [SerializeField] private float triggerThreshold = 0.55f;
        [SerializeField] private bool allowKeyboardDebugGate;
        [SerializeField] private Key keyboardDebugKey = Key.G;

        [Header("Sampling")]
        [SerializeField] private int minimumPointCount = 8;
        [SerializeField] private int maximumPointCount = 256;
        [SerializeField] private float minimumSampleDistance = 0.015f;

        [Header("Punch to activate")]
        [SerializeField] private Transform leftPunchHand;
        [SerializeField] private float minimumPunchSpeed = 0.8f;
        [SerializeField] private float signLifetime = 20f;
        [SerializeField] private int maximumStrokeCount = 4;
        private readonly List<AirGestureStroke> strokes = new List<AirGestureStroke>();
        private NetworkObject networkObject;
        private Bounds signBounds;
        private float punchReadyTime;
        private float lastStrokeTime;
        private float feedbackUntil;
        private bool awaitingResolution;
        private HandSample leftSample;
        private HandSample rightSample;

        private struct HandSample
        {
            public bool Valid;
            public Vector3 World;
            public Vector3 Local;
        }

        private readonly List<Vector3> points = new List<Vector3>(256);
        private bool recording;
        private float startTime;
        private Vector3 planeOrigin;
        private Vector3 planeRight = Vector3.right;
        private Vector3 planeUp = Vector3.up;

        public event Action<AirGestureStroke> StrokeCompleted;
        public event Action StrokeCanceled;
        public event Action<IReadOnlyList<AirGestureStroke>> SignSubmitted;
        public event Action<bool> SignResolved;

        public bool IsRecording => recording;
        public IReadOnlyList<Vector3> CurrentPoints => points;
        public IReadOnlyList<AirGestureStroke> Strokes => strokes;
        public bool ShowingFeedback => Time.time < feedbackUntil;
        public bool LastSignAccepted { get; private set; }
        public bool CanReadLocalInput => networkObject == null ||
            (networkObject.IsValid && networkObject.HasStateAuthority);

        private void Awake()
        {
            networkObject = GetComponentInParent<NetworkObject>();
        }

        private void Update()
        {
            if (drawPoint == null || !CanReadLocalInput)
            {
                CancelStroke();
                ClearSign();
                return;
            }

            if (!recording && ((feedbackUntil > 0f && !ShowingFeedback) ||
                (strokes.Count > 0 && Time.time - lastStrokeTime > signLifetime)))
            {
                ClearSign();
            }

            var gatePressed = IsDrawGatePressed();
            if (gatePressed)
            {
                if (!recording)
                {
                    BeginStroke();
                }

                SamplePoint();
                return;
            }

            if (recording)
            {
                CompleteStroke();
            }
        }

        private void OnDisable()
        {
            CancelStroke();
            ClearSign();
        }

        private void BeginStroke()
        {
            if (feedbackUntil > 0f || strokes.Count >= maximumStrokeCount)
            {
                ClearSign();
            }
            recording = true;
            points.Clear();
            startTime = Time.time;
            if (strokes.Count == 0)
            {
                planeOrigin = drawPoint.position;
                var reference = viewReference != null ? viewReference : drawPoint;
                planeRight = reference.right.normalized;
                planeUp = reference.up.normalized;
            }
            SamplePoint(true);
        }

        private void SamplePoint(bool force = false)
        {
            if (points.Count >= maximumPointCount)
            {
                return;
            }

            var position = drawPoint.position;
            if (!force && points.Count > 0 && Vector3.Distance(points[points.Count - 1], position) < minimumSampleDistance)
            {
                return;
            }

            points.Add(position);
        }

        private void CompleteStroke()
        {
            recording = false;
            if (points.Count < minimumPointCount)
            {
                points.Clear();
                StrokeCanceled?.Invoke();
                return;
            }

            var stroke = new AirGestureStroke(
                points.ToArray(),
                Time.time - startTime,
                planeOrigin,
                planeRight,
                planeUp);
            points.Clear();
            strokes.Add(stroke);
            lastStrokeTime = Time.time;
            punchReadyTime = Time.time + 0.25f;
            RebuildBounds();
            StrokeCompleted?.Invoke(stroke);
        }

        private Vector3 ToSignSpace(Vector3 point)
        {
            var offset = point - planeOrigin;
            return new Vector3(Vector3.Dot(offset, planeRight), Vector3.Dot(offset, planeUp),
                Vector3.Dot(offset, Vector3.Cross(planeRight, planeUp)));
        }

        private void RebuildBounds()
        {
            signBounds = new Bounds(ToSignSpace(strokes[0].WorldPoints[0]), Vector3.zero);
            foreach (var stroke in strokes)
                foreach (var point in stroke.WorldPoints)
                    signBounds.Encapsulate(ToSignSpace(point));
            // The filled rectangle includes the center of a circle, with fist-sized padding.
            signBounds.Expand(new Vector3(0.12f, 0.12f, 0.16f));
        }

        private void LateUpdate()
        {
            var canPunch = CanReadLocalInput && !recording && !ShowingFeedback &&
                strokes.Count > 0 && Time.time >= punchReadyTime && !IsDrawGatePressed();
            var hitLeft = CheckHand(leftPunchHand, XRNode.LeftHand, ref leftSample, canPunch);
            var hitRight = CheckHand(drawPoint, inputHand, ref rightSample, canPunch);
            if (!hitLeft && !hitRight) return;
            SubmitSign();
        }

        private void SubmitSign()
        {
            if (!CanReadLocalInput || recording || ShowingFeedback || strokes.Count == 0) return;
            awaitingResolution = true;
            SignSubmitted?.Invoke(strokes);
            if (awaitingResolution) ResolveSign(false);
        }

        private bool CheckHand(Transform hand, XRNode node, ref HandSample previous, bool canPunch)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (hand == null || !CanReadLocalInput || !device.isValid ||
                !device.TryGetFeatureValue(XRCommonUsages.isTracked, out var tracked) || !tracked)
            {
                previous.Valid = false;
                return false;
            }

            var world = hand.position;
            var local = transform.InverseTransformPoint(world);
            var deltaTime = Time.deltaTime;
            var speed = previous.Valid && deltaTime > 0f ? (local - previous.Local).magnitude / deltaTime : 0f;
            var hit = previous.Valid && deltaTime <= 0.1f && canPunch && speed >= minimumPunchSpeed &&
                SegmentEntersBounds(signBounds, ToSignSpace(previous.World), ToSignSpace(world));
            previous = new HandSample { Valid = true, World = world, Local = local };
            return hit;
        }

        public static bool SegmentEntersBounds(Bounds bounds, Vector3 from, Vector3 to)
        {
            if (bounds.Contains(from)) return false;
            var delta = to - from;
            return delta.sqrMagnitude > 0.000001f &&
                bounds.IntersectRay(new Ray(from, delta.normalized), out var distance) && distance <= delta.magnitude;
        }

        public void ResolveSign(bool accepted)
        {
            if (!awaitingResolution) return;
            awaitingResolution = false;
            LastSignAccepted = accepted;
            feedbackUntil = Time.time + (accepted ? 0.25f : 0.7f);
            SignResolved?.Invoke(accepted);
        }

        public void ClearSign()
        {
            strokes.Clear();
            feedbackUntil = 0f;
            awaitingResolution = false;
            leftSample.Valid = false;
            rightSample.Valid = false;
        }

        private void CancelStroke()
        {
            if (!recording && points.Count == 0)
            {
                return;
            }

            recording = false;
            points.Clear();
            StrokeCanceled?.Invoke();
        }

        private bool IsDrawGatePressed()
        {
            if (allowKeyboardDebugGate && Keyboard.current != null)
            {
                var keyControl = Keyboard.current[keyboardDebugKey];
                if (keyControl != null && keyControl.isPressed)
                {
                    return true;
                }
            }

            if (!requireTrigger)
            {
                return true;
            }

            var device = InputDevices.GetDeviceAtXRNode(inputHand);
            if (!device.isValid)
            {
                return false;
            }

            if (device.TryGetFeatureValue(XRCommonUsages.triggerButton, out var triggerButton) && triggerButton)
            {
                return true;
            }

            return device.TryGetFeatureValue(XRCommonUsages.trigger, out var triggerAmount) &&
                   triggerAmount >= triggerThreshold;
        }
    }
}
