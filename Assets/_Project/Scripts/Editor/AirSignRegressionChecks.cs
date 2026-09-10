using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Wapawapa.Gestures;

namespace Wapawapa.Editor
{
    public static class AirSignRegressionChecks
    {
        private const string CheckKey = "Wapawapa.AirSignChecks.v3";

        [InitializeOnLoadMethod]
        private static void ScheduleChecks()
        {
            if (SessionState.GetBool(CheckKey, false)) return;
            EditorApplication.update += RunWhenIdle;
        }

        private static void RunWhenIdle()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            EditorApplication.update -= RunWhenIdle;
            SessionState.SetBool(CheckKey, true);
            Run();
        }

        [MenuItem("Wapawapa/Tests/Check Air Signs")]
        public static void Run()
        {
            var root = new GameObject("Air Sign Regression Checks") { hideFlags = HideFlags.HideAndDontSave };
            var checks = 0;
            try
            {
                var recognizer = root.AddComponent<AirGestureRecognizer>();
                var he = Polyline(new Vector2(-0.2f, -0.02f), new Vector2(-0.072f, 0.12f), new Vector2(0.2f, -0.1f));
                var circle = Circle(new Vector2(0.14f, 0.19f), 0.05f);
                Check(recognizer.RecognizeSign(new[] { Stroke(he), Stroke(circle) }).GestureId == "pe", "pe", ref checks);
                Check(recognizer.RecognizeSign(new[] { Stroke(circle) }).GestureId == "circle", "circle stays circle", ref checks);
                Check(recognizer.RecognizeSign(new[] { Stroke(he) }).GestureId != "pe", "he alone is not pe", ref checks);
                Check(!recognizer.RecognizeSign(new[] { Stroke(he), Stroke(Circle(new Vector2(-0.25f, 0.2f), 0.05f)) }).Succeeded, "mark on left", ref checks);
                Check(!recognizer.RecognizeSign(new[] { Stroke(he), Stroke(Circle(new Vector2(0.14f, -0.2f), 0.05f)) }).Succeeded, "mark below", ref checks);
                Check(!recognizer.RecognizeSign(new[] { Stroke(he), Stroke(Circle(new Vector2(0.14f, 0.2f), 0.2f)) }).Succeeded, "oversized mark", ref checks);
                Check(!recognizer.RecognizeSign(new[] { Stroke(he), Stroke(he) }).Succeeded, "two open strokes", ref checks);
                Check(!recognizer.RecognizeSign(new[] { Stroke(he), Stroke(circle), Stroke(circle) }).Succeeded, "extra stroke", ref checks);
                var rotation = Quaternion.Euler(10f, 125f, 0f);
                var origin = new Vector3(5f, 2f, -3f);
                Check(recognizer.RecognizeSign(new[] { Stroke(he, origin, rotation), Stroke(circle, origin, rotation) }).GestureId == "pe", "translated rotated plane", ref checks);
                var triangle = Polyline(new Vector2(0, 0.232f), new Vector2(-0.22f, -0.168f), new Vector2(0.22f, -0.168f), new Vector2(0, 0.232f));
                Check(recognizer.RecognizeSign(new[] { Stroke(triangle) }).GestureId == "triangle", "triangle preserved", ref checks);
                var bounds = new Bounds(Vector3.zero, new Vector3(0.5f, 0.5f, 0.16f));
                Check(AirGestureRecorder.SegmentEntersBounds(bounds, Vector3.back, Vector3.forward), "fast punch through center", ref checks);
                Check(!AirGestureRecorder.SegmentEntersBounds(bounds, Vector3.zero, Vector3.forward), "hand already inside", ref checks);
                Check(!AirGestureRecorder.SegmentEntersBounds(bounds, Vector3.back + Vector3.right, Vector3.forward + Vector3.right), "miss outside bounds", ref checks);
                var recorder = root.AddComponent<AirGestureRecorder>();
                SetField(recorder, "drawPoint", root.transform);
                SetField(recognizer, "recorder", recorder);
                Invoke(recognizer, "OnEnable");
                var activations = 0;
                recognizer.GestureRecognized += result =>
                {
                    activations++;
                    recognizer.ConfirmActivation(true);
                };
                Record(recorder, he);
                Check(recorder.Strokes.Count == 1 && activations == 0, "release preserves first stroke without activation", ref checks);
                Record(recorder, circle);
                Check(recorder.Strokes.Count == 2 && activations == 0, "second release still waits for punch", ref checks);
                Invoke(recorder, "SubmitSign");
                Check(activations == 1 && recorder.ShowingFeedback && recorder.LastSignAccepted, "punch activates pe", ref checks);
                Invoke(recorder, "SubmitSign");
                Check(activations == 1, "same sign cannot fire twice", ref checks);
                recorder.ClearSign();
                Check(recorder.Strokes.Count == 0 && !recorder.ShowingFeedback, "clear resets session", ref checks);
                Record(recorder, he);
                Record(recorder, Circle(new Vector2(-0.25f, 0.2f), 0.05f));
                Invoke(recorder, "SubmitSign");
                Check(activations == 1 && recorder.ShowingFeedback && !recorder.LastSignAccepted, "bad sign rejects on punch", ref checks);
                Invoke(recorder, "OnDisable");
                Check(recorder.Strokes.Count == 0, "disable clears sign", ref checks);
                var view = root.AddComponent<AirGestureDebugView>();
                SetField(view, "recorder", recorder);
                Invoke(view, "Awake");
                Invoke(view, "LateUpdate");
                Check(root.GetComponentsInChildren<LineRenderer>().Length == 1, "idle trail initializes", ref checks);
                Record(recorder, he);
                Record(recorder, circle);
                Invoke(view, "LateUpdate");
                var visiblePoints = 0;
                foreach (var line in root.GetComponentsInChildren<LineRenderer>()) visiblePoints += line.positionCount;
                Check(visiblePoints == he.Length + circle.Length, "released strokes remain visible", ref checks);
                Invoke(view, "OnDisable");
                SetField(view, "lineProperties", null);
                Invoke(view, "OnEnable");
                Invoke(view, "LateUpdate");
                Check(root.GetComponentsInChildren<LineRenderer>().Length == 3, "reload restores rendering without duplicate lines", ref checks);
                Debug.Log($"Air sign regression checks PASSED ({checks}).");
            }
            catch (Exception exception)
            {
                Debug.LogError($"Air sign regression checks FAILED after {checks}: {exception}");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Check(bool condition, string label, ref int count)
        {
            if (!condition) throw new InvalidOperationException(label);
            count++;
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private static void Invoke(object target, string name) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private static void Record(AirGestureRecorder recorder, Vector2[] stroke)
        {
            Invoke(recorder, "BeginStroke");
            var points = (List<Vector3>)typeof(AirGestureRecorder).GetField("points", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(recorder);
            points.Clear();
            foreach (var point in stroke) points.Add(point);
            SetField(recorder, "startTime", Time.time - 1f);
            Invoke(recorder, "CompleteStroke");
        }

        private static Vector2[] Polyline(params Vector2[] corners)
        {
            var points = new List<Vector2>();
            for (var i = 1; i < corners.Length; i++)
                for (var j = 0; j < 16; j++) points.Add(Vector2.Lerp(corners[i - 1], corners[i], j / 16f));
            points.Add(corners[corners.Length - 1]);
            return points.ToArray();
        }

        private static Vector2[] Circle(Vector2 center, float radius)
        {
            var points = new Vector2[33];
            for (var i = 0; i < points.Length; i++)
            {
                var angle = i * Mathf.PI * 2 / 32;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        private static AirGestureStroke Stroke(Vector2[] points) => Stroke(points, Vector3.zero, Quaternion.identity);

        private static AirGestureStroke Stroke(Vector2[] points, Vector3 origin, Quaternion rotation)
        {
            var world = new Vector3[points.Length];
            for (var i = 0; i < world.Length; i++) world[i] = origin + rotation * (Vector3)points[i];
            return new AirGestureStroke(world, 1f, origin, rotation * Vector3.right, rotation * Vector3.up);
        }
    }
}
