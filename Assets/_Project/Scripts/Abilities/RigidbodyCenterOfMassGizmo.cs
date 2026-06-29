using UnityEngine;

namespace Wapawapa.Abilities
{
    [ExecuteAlways]
    public sealed class RigidbodyCenterOfMassGizmo : MonoBehaviour
    {
        [Tooltip("重心を描画する対象のRigidbodyです。未設定なら同じGameObjectから自動取得します。")]
        [SerializeField] private Rigidbody targetRigidbody;

        [Tooltip("Scene Viewに表示する重心ギズモの色です。")]
        [SerializeField] private Color gizmoColor = Color.yellow;

        [Tooltip("Scene Viewに表示する重心ギズモの大きさです。")]
        [Min(0.01f)]
        [SerializeField] private float gizmoSize = 0.12f;

        private void Reset()
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }

        private void OnValidate()
        {
            if (targetRigidbody == null)
            {
                targetRigidbody = GetComponent<Rigidbody>();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (targetRigidbody == null)
            {
                targetRigidbody = GetComponent<Rigidbody>();
            }

            if (targetRigidbody == null)
            {
                return;
            }

            var worldCenter = targetRigidbody.transform.TransformPoint(targetRigidbody.centerOfMass);
            var axisSize = gizmoSize * 2f;

            Gizmos.color = gizmoColor;
            Gizmos.DrawSphere(worldCenter, gizmoSize);
            Gizmos.DrawLine(worldCenter + Vector3.left * axisSize, worldCenter + Vector3.right * axisSize);
            Gizmos.DrawLine(worldCenter + Vector3.down * axisSize, worldCenter + Vector3.up * axisSize);
            Gizmos.DrawLine(worldCenter + Vector3.back * axisSize, worldCenter + Vector3.forward * axisSize);
        }
    }
}
