namespace Wapawapa.Gestures
{
    public readonly struct AirGestureResult
    {
        public AirGestureResult(string gestureId, float confidence, float score, int pointCount)
        {
            GestureId = gestureId;
            Confidence = confidence;
            Score = score;
            PointCount = pointCount;
        }

        public string GestureId { get; }
        public float Confidence { get; }
        public float Score { get; }
        public int PointCount { get; }
        public bool Succeeded => !string.IsNullOrEmpty(GestureId);

        public static AirGestureResult Failed(int pointCount = 0)
        {
            return new AirGestureResult(string.Empty, 0f, float.PositiveInfinity, pointCount);
        }
    }
}
