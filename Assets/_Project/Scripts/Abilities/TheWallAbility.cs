using System.Collections;
using UnityEngine;

namespace Wapawapa.Abilities
{
    public sealed class TheWallAbility : AbilityBase
    {
        [System.Serializable]
        private sealed class 壁モデル設定
        {
            [Tooltip("地面から生やすモデルPrefabです。")]
            [SerializeField] private GameObject 壁モデルPrefab;
            [Tooltip("このモデルだけにかける向き補正です。モデルが倒れて出る場合に調整します。")]
            [SerializeField] private Vector3 回転補正 = Vector3.zero;
            [Tooltip("このモデルの大きさ倍率です。")]
            [SerializeField] private float 壁スケール = 1f;
            [Tooltip("このモデルが地面から生えきるまでの時間です。")]
            [SerializeField] private float 生える時間 = 0.45f;
            [Tooltip("このモデルが残る時間です。")]
            [SerializeField] private float 持続時間 = 6f;
            [Tooltip("このモデルを生える前に地面へ隠しておく深さです。")]
            [SerializeField] private float 地面下の深さ = 2.4f;
            [Tooltip("このモデルにColliderが無い時だけ、保険として当たり判定を追加します。")]
            [SerializeField] private bool Colliderが無い時だけ補助Colliderを追加 = true;
            [Tooltip("保険で追加する当たり判定の大きさです。Prefab側にColliderがある場合は使われません。")]
            [SerializeField] private Vector3 補助Colliderサイズ = new Vector3(1.8f, 3.2f, 0.8f);
            [Tooltip("保険で追加する当たり判定の中心位置です。Prefab側にColliderがある場合は使われません。")]
            [SerializeField] private Vector3 補助Collider中心 = new Vector3(0f, 1.6f, 0f);
            [Tooltip("このモデルが生える時に少し揺れる強さです。")]
            [SerializeField] private float 揺れ幅 = 0.04f;

            public GameObject Prefab => 壁モデルPrefab;
            public Vector3 RotationOffset => 回転補正;
            public float Scale => Mathf.Max(0.01f, 壁スケール);
            public float GrowDuration => Mathf.Max(0.01f, 生える時間);
            public float Lifetime => Mathf.Max(0.1f, 持続時間);
            public float UndergroundDepth => Mathf.Max(0f, 地面下の深さ);
            public bool AddFallbackCollider => Colliderが無い時だけ補助Colliderを追加;
            public Vector3 FallbackColliderSize => 補助Colliderサイズ;
            public Vector3 FallbackColliderCenter => 補助Collider中心;
            public float ShakeAmount => Mathf.Max(0f, 揺れ幅);
        }

        [Header("生成設定")]
        [Tooltip("地面から生やすモデル設定です。複数入れるとランダムで1つ選ばれます。")]
        [SerializeField] private 壁モデル設定[] 壁モデル設定リスト;
        [Tooltip("プレイヤーの前方何mに壁を出すかです。")]
        [SerializeField] private float 生成距離 = 2.2f;
        [Tooltip("地面を探すために上からRayを飛ばす高さです。")]
        [SerializeField] private float 地面探索高さ = 3f;
        [Tooltip("地面を探すRayの長さです。")]
        [SerializeField] private float 地面探索距離 = 8f;
        [Tooltip("地面として扱うレイヤーです。")]
        [SerializeField] private LayerMask 地面レイヤー = ~0;

        protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
        {
            var forward = FlattenDirection(activation.Direction);
            var spawnPoint = FindGroundPoint(activation.Origin + forward * 生成距離);
            var model = ChooseModel();
            var rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(model.RotationOffset);
            var wall = CreateWallObject(spawnPoint, rotation, model);

            StartCoroutine(GrowAndDestroy(wall, spawnPoint, model));
        }

        private GameObject CreateWallObject(Vector3 spawnPoint, Quaternion rotation, 壁モデル設定 model)
        {
            GameObject wall;
            var startPosition = spawnPoint - Vector3.up * model.UndergroundDepth;

            if (model.Prefab != null)
            {
                wall = Instantiate(model.Prefab, startPosition, rotation);
            }
            else
            {
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetPositionAndRotation(startPosition, rotation);
                wall.transform.localScale = new Vector3(1.8f, 3.2f, 0.8f);
            }

            wall.name = "The Wall";
            wall.transform.localScale *= model.Scale;

            if (model.AddFallbackCollider && wall.GetComponentInChildren<Collider>() == null)
            {
                var shieldCollider = wall.AddComponent<BoxCollider>();
                shieldCollider.size = model.FallbackColliderSize;
                shieldCollider.center = model.FallbackColliderCenter;
                shieldCollider.isTrigger = false;
            }

            var rigidbody = wall.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = wall.AddComponent<Rigidbody>();
            }

            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;

            if (wall.GetComponent<TheWallShield>() == null)
            {
                wall.AddComponent<TheWallShield>();
            }

            return wall;
        }

        private 壁モデル設定 ChooseModel()
        {
            if (壁モデル設定リスト == null || 壁モデル設定リスト.Length == 0)
            {
                return new 壁モデル設定();
            }

            var startIndex = Random.Range(0, 壁モデル設定リスト.Length);
            for (var i = 0; i < 壁モデル設定リスト.Length; i++)
            {
                var index = (startIndex + i) % 壁モデル設定リスト.Length;
                var model = 壁モデル設定リスト[index];
                if (model != null && model.Prefab != null)
                {
                    return model;
                }
            }

            return new 壁モデル設定();
        }

        private Vector3 FindGroundPoint(Vector3 aroundPoint)
        {
            var rayOrigin = aroundPoint + Vector3.up * 地面探索高さ;
            if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, 地面探索距離, 地面レイヤー, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return aroundPoint;
        }

        private static Vector3 FlattenDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector3.forward;
            }

            return direction.normalized;
        }

        private IEnumerator GrowAndDestroy(GameObject wall, Vector3 finalPosition, 壁モデル設定 model)
        {
            if (wall == null)
            {
                yield break;
            }

            var startPosition = finalPosition - Vector3.up * model.UndergroundDepth;
            var elapsed = 0f;

            while (elapsed < model.GrowDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / model.GrowDuration);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                var shake = Mathf.Sin(t * Mathf.PI * 6f) * model.ShakeAmount * (1f - t);
                wall.transform.position = Vector3.Lerp(startPosition, finalPosition, eased) + wall.transform.right * shake;
                yield return null;
            }

            wall.transform.position = finalPosition;
            Destroy(wall, model.Lifetime);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 0.7f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position + transform.forward * 生成距離, 0.35f);
        }
    }

    internal sealed class TheWallShield : MonoBehaviour, IAbilityDamageReceiver
    {
        public void ApplyDamage(in AbilityDamage damage)
        {
            // Damage is intentionally absorbed so projectile-style abilities stop at the wall.
        }
    }
}
