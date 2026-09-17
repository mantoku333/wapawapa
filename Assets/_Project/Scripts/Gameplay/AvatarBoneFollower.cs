using UnityEngine;

namespace Wapawapa.Gameplay
{
    /// <summary>Bind-pose-relative follower for the imported avatar's accessory/twist bones.</summary>
    public sealed class AvatarBoneFollower : MonoBehaviour
    {
        [SerializeField] private Transform[] sources;
        [SerializeField] private float[] sourceWeights;
        [SerializeField, Range(0f, 1f)] private float weight = 1f;
        [SerializeField] private bool followPosition;
        private Quaternion[] rotationOffsets;
        private Vector3[] positionOffsets;
        private bool initialized;

        public void Initialize()
        {
            if (initialized || sources == null || sources.Length == 0) return;
            rotationOffsets = new Quaternion[sources.Length];
            positionOffsets = new Vector3[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] == null) continue;
                rotationOffsets[i] = Quaternion.Inverse(sources[i].rotation) * transform.rotation;
                positionOffsets[i] = sources[i].InverseTransformPoint(transform.position);
            }
            initialized = true;
        }
        public void Apply()
        {
            if (!initialized) Initialize();
            if (!initialized) return;
            Quaternion rotation = transform.rotation;
            Vector3 position = Vector3.zero;
            float total = 0f;
            for (int i = 0; i < sources.Length; i++)
            {
                float w = sourceWeights != null && i < sourceWeights.Length ? sourceWeights[i] : 1f;
                if (sources[i] == null || w <= 0f) continue;
                total += w;
                rotation = Quaternion.Slerp(rotation, sources[i].rotation * rotationOffsets[i], w / total);
                position += sources[i].TransformPoint(positionOffsets[i]) * w;
            }
            if (total <= 0f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, weight);
            if (followPosition) transform.position = Vector3.Lerp(transform.position, position / total, weight);
        }
    }
}
