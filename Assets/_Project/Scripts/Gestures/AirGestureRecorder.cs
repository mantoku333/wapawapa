using System;
using System.Collections.Generic;
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

        private readonly List<Vector3> points = new List<Vector3>(256);
        private bool recording;
        private float startTime;
        private Vector3 planeOrigin;
        private Vector3 planeRight = Vector3.right;
        private Vector3 planeUp = Vector3.up;

        public event Action<AirGestureStroke> StrokeCompleted;
        public event Action StrokeCanceled;

        public bool IsRecording => recording;
        public IReadOnlyList<Vector3> CurrentPoints => points;

        private void Update()
        {
            if (drawPoint == null)
            {
                CancelStroke();
                return;
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
        }

        private void BeginStroke()
        {
            recording = true;
            points.Clear();
            startTime = Time.time;
            planeOrigin = drawPoint.position;

            var reference = viewReference != null ? viewReference : drawPoint;
            planeRight = reference.right.normalized;
            planeUp = reference.up.normalized;
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
            StrokeCompleted?.Invoke(stroke);
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
