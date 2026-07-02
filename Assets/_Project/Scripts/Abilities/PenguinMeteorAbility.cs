using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Wapawapa.Abilities
{
    public sealed class PenguinMeteorAbility : AbilityBase, IHoldAbility
    {
        private const string PenguinPrefabPath = "Assets/_Project/Prefabs/Abilities/MeteorPenguen.prefab";
        private const string PenguinControllerPath = "Assets/Hosh/Stylized Penguin (Free)/Art/Animations/Penguen_Emperor_Lite.controller";
        private const string PenguinSwimClipPath = "Assets/Hosh/Stylized Penguin (Free)/Art/Animations/Swim_Emperor_Lite.anim";

        [Header("Target Area")]
        [Tooltip("指定する円状範囲の半径です。大きいほど広い範囲にペンギンが降ります。")]
        [Min(0.1f)]
        [SerializeField] private float targetRadius = 2.5f;
        [Tooltip("指定範囲のふちの線の色です。")]
        [SerializeField] private Color targetEdgeColor = Color.red;
        [Tooltip("指定範囲の内側の色です。アルファ値を下げると透明になります。")]
        [SerializeField] private Color targetFillColor = new Color(1f, 0f, 0f, 0.22f);
        [Tooltip("範囲指定用のRayが届く最大距離です。")]
        [Min(0.1f)]
        [SerializeField] private float maxAimDistance = 30f;
        [Tooltip("Rayが床に当たらない時、前方何メートル先に仮の範囲を置くかです。")]
        [Min(0.1f)]
        [SerializeField] private float fallbackTargetDistance = 6f;
        [Tooltip("ONにすると手からのRayではなく、マウスカーソル位置から範囲を指定します。テストシーン用です。")]
        [SerializeField] private bool useMouseCursorTargeting;
        [Tooltip("マウス指定に使うカメラです。未設定ならOwner配下のCamera、次にMainCameraを探します。")]
        [SerializeField] private Camera mouseTargetCamera;
        [Tooltip("範囲指定のRayが当たる床や地形のレイヤーです。")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Meteor Spawn")]
        [Tooltip("発動してからペンギンが降り始めるまでの待ち時間です。")]
        [Min(0f)]
        [SerializeField] private float activationDelay = 0.5f;
        [Tooltip("指定した円から見て、ペンギンが生成される高さです。")]
        [Min(0.1f)]
        [SerializeField] private float meteorHeight = 7f;
        [Tooltip("ペンギンが降ってくる斜め角度です。90度に近いほど真上から降ります。")]
        [Range(1f, 89f)]
        [SerializeField] private float meteorAngleDegrees = 60f;
        [Tooltip("ONにすると、カメラから見て右上の方向からペンギンが降ります。")]
        [SerializeField] private bool spawnFromCameraViewRight = true;
        [Tooltip("一度の発動で降らせるペンギンの数です。")]
        [Min(1)]
        [SerializeField] private int penguinCount = 8;
        [Tooltip("全ペンギンを何秒かけて連続生成するかです。0にするとほぼ同時に出ます。")]
        [Min(0f)]
        [SerializeField] private float emissionDuration = 1.2f;
        [Tooltip("生成直後にペンギンへ与える進行方向の初速です。")]
        [Min(0.1f)]
        [SerializeField] private float launchSpeed = 12f;

        [Header("Damage")]
        [Tooltip("ペンギン1匹が命中した時に与えるダメージです。")]
        [Min(0f)]
        [SerializeField] private float damagePerPenguin = 5f;
        [Tooltip("命中時に相手を押す強さです。")]
        [Min(0f)]
        [SerializeField] private float pushForce = 3f;
        [Tooltip("ダメージ判定の対象にするレイヤーです。")]
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Visual")]
        [Tooltip("降らせるペンギンのPrefabです。")]
        [SerializeField] private GameObject penguinPrefab;
        [Tooltip("生成したペンギンに割り当てるAnimator Controllerです。")]
        [SerializeField] private RuntimeAnimatorController penguinAnimatorController;
        [Tooltip("生成したペンギンにループ再生させる泳ぎアニメーションです。")]
        [SerializeField] private AnimationClip penguinSwimAnimation;
        [Tooltip("ペンギンの頭が進行方向を向かない時に、見た目の向きを補正する回転角度です。")]
        [SerializeField] private Vector3 penguinForwardEulerOffset;
        [Tooltip("生成したペンギンを自動で消すまでの秒数です。")]
        [Min(0.1f)]
        [SerializeField] private float penguinLifetime = 8f;

        private TargetIndicator indicator;
        private bool hasTarget;
        private Vector3 targetCenter;
        private Vector3 targetNormal = Vector3.up;
        private Vector3 targetForward = Vector3.forward;

        public void BeginHold(in AbilityContext context)
        {
            EnsureIndicator();
            UpdateTarget(context);
        }

        public void UpdateHold(in AbilityContext context)
        {
            EnsureIndicator();
            UpdateTarget(context);
        }

        public void EndHold(in AbilityContext context, bool activate)
        {
            if (indicator != null)
            {
                indicator.SetVisible(false);
            }
        }

        protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
        {
            if (!hasTarget)
            {
                UpdateTarget(context);
            }

            if (!hasTarget)
            {
                return;
            }

            StartCoroutine(SpawnMeteorRoutine(context.Owner, activation.AbilityId, targetCenter, targetNormal, targetForward));
        }

        private void UpdateTarget(in AbilityContext context)
        {
            hasTarget = TryFindTarget(context, out targetCenter, out targetNormal, out targetForward);

            if (indicator != null)
            {
                indicator.SetVisible(hasTarget);
                if (hasTarget)
                {
                    indicator.UpdateDisplay(targetCenter, targetNormal, targetRadius, targetEdgeColor, targetFillColor);
                }
            }
        }

        private bool TryFindTarget(in AbilityContext context, out Vector3 center, out Vector3 normal, out Vector3 forward)
        {
            var ray = CreateTargetRay(context);
            var hits = Physics.RaycastAll(ray, maxAimDistance, groundMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (context.Owner != null && hit.transform.IsChildOf(context.Owner.transform))
                {
                    continue;
                }

                center = hit.point;
                normal = hit.normal.sqrMagnitude > 0f ? hit.normal.normalized : Vector3.up;
                forward = Vector3.ProjectOnPlane(ray.direction, normal);
                if (forward.sqrMagnitude <= 0.0001f)
                {
                    forward = Vector3.ProjectOnPlane(context.AimSource.forward, normal);
                }

                forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
                forward = GetMeteorApproachForward(context, normal, forward);
                return true;
            }

            var planeY = context.Owner != null ? context.Owner.transform.position.y : 0f;
            var plane = new Plane(Vector3.up, new Vector3(0f, planeY, 0f));
            if (plane.Raycast(ray, out var distance) && distance > 0f && distance <= maxAimDistance)
            {
                center = ray.GetPoint(distance);
                normal = Vector3.up;
                forward = Vector3.ProjectOnPlane(ray.direction, normal);
                forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
                forward = GetMeteorApproachForward(context, normal, forward);
                return true;
            }

            var fallbackForward = Vector3.ProjectOnPlane(ray.direction, Vector3.up);
            if (fallbackForward.sqrMagnitude <= 0.0001f)
            {
                fallbackForward = Vector3.ProjectOnPlane(context.AimSource.forward, Vector3.up);
            }

            if (fallbackForward.sqrMagnitude <= 0.0001f && context.Owner != null)
            {
                fallbackForward = Vector3.ProjectOnPlane(context.Owner.transform.forward, Vector3.up);
            }

            if (fallbackForward.sqrMagnitude > 0.0001f)
            {
                fallbackForward.Normalize();
                center = ray.origin + fallbackForward * Mathf.Min(fallbackTargetDistance, maxAimDistance);
                center.y = planeY;
                normal = Vector3.up;
                forward = GetMeteorApproachForward(context, normal, fallbackForward);
                return true;
            }

            center = default;
            normal = Vector3.up;
            forward = Vector3.forward;
            return false;
        }

        private Ray CreateTargetRay(in AbilityContext context)
        {
            if (useMouseCursorTargeting && TryCreateMouseRay(context, out var mouseRay))
            {
                return mouseRay;
            }

            var raySource = context.RightHand != null ? context.RightHand : context.LeftHand != null ? context.LeftHand : context.AimSource;
            return new Ray(raySource.position, raySource.forward);
        }

        private bool TryCreateMouseRay(in AbilityContext context, out Ray ray)
        {
            var mouse = Mouse.current;
            var camera = GetTargetCamera(context);

            if (mouse == null || camera == null)
            {
                ray = default;
                return false;
            }

            ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            return true;
        }

        private Vector3 GetMeteorApproachForward(in AbilityContext context, Vector3 normal, Vector3 fallbackForward)
        {
            if (spawnFromCameraViewRight && TryGetCameraRightForward(context, normal, out var cameraRightForward))
            {
                return cameraRightForward;
            }

            return fallbackForward.sqrMagnitude > 0.0001f ? fallbackForward.normalized : Vector3.forward;
        }

        private bool TryGetCameraRightForward(in AbilityContext context, Vector3 normal, out Vector3 forward)
        {
            var camera = GetTargetCamera(context);
            if (camera == null)
            {
                forward = default;
                return false;
            }

            var cameraRight = Vector3.ProjectOnPlane(camera.transform.right, normal);
            if (cameraRight.sqrMagnitude <= 0.0001f)
            {
                forward = default;
                return false;
            }

            forward = -cameraRight.normalized;
            return true;
        }

        private Camera GetTargetCamera(in AbilityContext context)
        {
            if (mouseTargetCamera != null)
            {
                return mouseTargetCamera;
            }

            if (context.Owner != null)
            {
                var ownerCamera = context.Owner.GetComponentInChildren<Camera>();
                if (ownerCamera != null)
                {
                    return ownerCamera;
                }
            }

            return Camera.main;
        }

        private IEnumerator SpawnMeteorRoutine(GameObject owner, string abilityId, Vector3 center, Vector3 normal, Vector3 forward)
        {
            if (activationDelay > 0f)
            {
                yield return new WaitForSeconds(activationDelay);
            }

            var count = Mathf.Max(1, penguinCount);
            var interval = count <= 1 ? 0f : Mathf.Max(0f, emissionDuration) / (count - 1);
            var spawnCenter = CalculateSpawnCenter(center, normal, forward);
            var fallDirection = (center - spawnCenter).normalized;
            if (fallDirection.sqrMagnitude <= 0.0001f)
            {
                fallDirection = Vector3.down;
            }

            for (var i = 0; i < count; i++)
            {
                SpawnPenguin(owner, abilityId, spawnCenter + RandomPointInTargetDisk(normal), fallDirection);

                if (interval > 0f && i < count - 1)
                {
                    yield return new WaitForSeconds(interval);
                }
            }
        }

        private Vector3 CalculateSpawnCenter(Vector3 center, Vector3 normal, Vector3 forward)
        {
            var clampedAngle = Mathf.Clamp(meteorAngleDegrees, 1f, 89f) * Mathf.Deg2Rad;
            var horizontalOffset = meteorHeight / Mathf.Tan(clampedAngle);
            return center + normal * meteorHeight - forward.normalized * horizontalOffset;
        }

        private Vector3 RandomPointInTargetDisk(Vector3 normal)
        {
            var random = Random.insideUnitCircle * targetRadius;
            var tangent = Vector3.Cross(normal, Vector3.forward);
            if (tangent.sqrMagnitude <= 0.0001f)
            {
                tangent = Vector3.Cross(normal, Vector3.right);
            }

            tangent.Normalize();
            var bitangent = Vector3.Cross(normal, tangent).normalized;
            return tangent * random.x + bitangent * random.y;
        }

        private void SpawnPenguin(GameObject owner, string abilityId, Vector3 position, Vector3 fallDirection)
        {
            var rotation = Quaternion.LookRotation(fallDirection.sqrMagnitude > 0.0001f ? fallDirection.normalized : Vector3.down, Vector3.up)
                * Quaternion.Euler(penguinForwardEulerOffset);
            var penguin = penguinPrefab != null
                ? Instantiate(penguinPrefab, position, rotation)
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);

            penguin.name = "Penguin Meteor Projectile";
            EnsureAnimator(penguin);

            var collider = penguin.GetComponent<Collider>();
            if (collider == null)
            {
                collider = penguin.AddComponent<CapsuleCollider>();
            }

            var rigidbody = penguin.GetComponent<Rigidbody>();
            if (rigidbody == null)
            {
                rigidbody = penguin.AddComponent<Rigidbody>();
                rigidbody.useGravity = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            rigidbody.linearVelocity = fallDirection.normalized * launchSpeed;

            var projectile = penguin.GetComponent<PenguinMeteorProjectile>();
            if (projectile == null)
            {
                projectile = penguin.AddComponent<PenguinMeteorProjectile>();
            }

            projectile.Initialize(abilityId, owner, damagePerPenguin, pushForce, hitMask, penguinLifetime);
        }

        private void EnsureAnimator(GameObject penguin)
        {
            var animator = penguin.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = penguin.AddComponent<Animator>();
            }

            if (penguinAnimatorController != null)
            {
                animator.runtimeAnimatorController = CreateSwimOnlyController();
            }

            animator.enabled = true;
            if (animator.runtimeAnimatorController != null)
            {
                animator.Play("Swim_Emperor", 0, Random.value);
            }
        }

        private RuntimeAnimatorController CreateSwimOnlyController()
        {
            if (penguinAnimatorController == null || penguinSwimAnimation == null)
            {
                return penguinAnimatorController;
            }

            penguinSwimAnimation.wrapMode = WrapMode.Loop;

            var overrideController = new AnimatorOverrideController(penguinAnimatorController);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);

            for (var i = 0; i < overrides.Count; i++)
            {
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, penguinSwimAnimation);
            }

            overrideController.ApplyOverrides(overrides);
            return overrideController;
        }

        private void EnsureIndicator()
        {
            if (indicator != null)
            {
                return;
            }

            var indicatorObject = new GameObject("Penguin Meteor Target Indicator");
            indicatorObject.transform.SetParent(transform, false);
            indicator = indicatorObject.AddComponent<TargetIndicator>();
            indicator.Initialize();
        }

        private void OnDisable()
        {
            if (indicator != null)
            {
                indicator.SetVisible(false);
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            AutoAssignAssets();
        }

        private void OnValidate()
        {
            AutoAssignAssets();
        }

        private void AutoAssignAssets()
        {
            if (penguinPrefab == null)
            {
                penguinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PenguinPrefabPath);
            }

            if (penguinAnimatorController == null)
            {
                penguinAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PenguinControllerPath);
            }

            if (penguinSwimAnimation == null)
            {
                penguinSwimAnimation = AssetDatabase.LoadAssetAtPath<AnimationClip>(PenguinSwimClipPath);
            }
        }
#endif
    }

    internal sealed class PenguinMeteorProjectile : MonoBehaviour
    {
        private readonly HashSet<Transform> damagedRoots = new HashSet<Transform>();
        private readonly Collider[] overlapHits = new Collider[16];

        private string abilityId;
        private GameObject owner;
        private float damage;
        private float pushForce;
        private LayerMask hitMask;
        private float destroyTime;
        private Collider projectileCollider;

        public void Initialize(string abilityId, GameObject owner, float damage, float pushForce, LayerMask hitMask, float lifetime)
        {
            this.abilityId = abilityId;
            this.owner = owner;
            this.damage = damage;
            this.pushForce = pushForce;
            this.hitMask = hitMask;
            destroyTime = Time.time + Mathf.Max(0.1f, lifetime);
            projectileCollider = GetComponent<Collider>();
        }

        private void Update()
        {
            if (Time.time >= destroyTime)
            {
                Destroy(gameObject);
                return;
            }

            ScanOverlaps();
        }

        private void ScanOverlaps()
        {
            var radius = GetDamageRadius();
            var count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlapHits, hitMask, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var other = overlapHits[i];
                overlapHits[i] = null;

                if (other == projectileCollider)
                {
                    continue;
                }

                TryApplyDamage(other, other != null ? other.ClosestPoint(transform.position) : transform.position);
            }
        }

        private float GetDamageRadius()
        {
            if (projectileCollider == null)
            {
                projectileCollider = GetComponent<Collider>();
            }

            if (projectileCollider == null)
            {
                return 0.5f;
            }

            var extents = projectileCollider.bounds.extents;
            return Mathf.Max(0.2f, Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z)) * 0.9f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            var point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            TryApplyDamage(collision.collider, point);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryApplyDamage(other, other.ClosestPoint(transform.position));
        }

        private void TryApplyDamage(Collider other, Vector3 point)
        {
            if (other == null || ((1 << other.gameObject.layer) & hitMask.value) == 0)
            {
                return;
            }

            if (owner != null && other.transform.IsChildOf(owner.transform))
            {
                return;
            }

            var root = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform.root;
            if (root != null && !damagedRoots.Add(root))
            {
                return;
            }

            var direction = (other.transform.position - transform.position).normalized;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.down;
            }

            var damageData = new AbilityDamage(abilityId, damage, direction, pushForce, point, owner);
            AbilityDamageUtility.TryApplyDamage(other, damageData);
        }
    }

    internal sealed class TargetIndicator : MonoBehaviour
    {
        private const int Segments = 96;

        private MeshRenderer fillRenderer;
        private LineRenderer edgeRenderer;
        private Material fillMaterial;
        private Material edgeMaterial;

        public void Initialize()
        {
            var fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(transform, false);
            var meshFilter = fillObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateDiscMesh();
            fillRenderer = fillObject.AddComponent<MeshRenderer>();
            fillMaterial = CreateMaterial();
            fillRenderer.sharedMaterial = fillMaterial;

            var edgeObject = new GameObject("Edge");
            edgeObject.transform.SetParent(transform, false);
            edgeRenderer = edgeObject.AddComponent<LineRenderer>();
            edgeRenderer.useWorldSpace = false;
            edgeRenderer.loop = true;
            edgeRenderer.positionCount = Segments;
            edgeRenderer.widthMultiplier = 0.04f;
            edgeMaterial = CreateMaterial();
            edgeRenderer.sharedMaterial = edgeMaterial;

            for (var i = 0; i < Segments; i++)
            {
                var radians = i / (float)Segments * Mathf.PI * 2f;
                edgeRenderer.SetPosition(i, new Vector3(Mathf.Cos(radians), 0.01f, Mathf.Sin(radians)));
            }

            SetVisible(false);
        }

        public void UpdateDisplay(Vector3 center, Vector3 normal, float radius, Color edgeColor, Color fillColor)
        {
            transform.SetPositionAndRotation(center + normal * 0.02f, Quaternion.FromToRotation(Vector3.up, normal));
            transform.localScale = new Vector3(radius, 1f, radius);
            SetMaterialColor(fillMaterial, fillColor);
            SetMaterialColor(edgeMaterial, edgeColor);
        }

        public void SetVisible(bool visible)
        {
            if (fillRenderer != null)
            {
                fillRenderer.enabled = visible;
            }

            if (edgeRenderer != null)
            {
                edgeRenderer.enabled = visible;
            }
        }

        private static Mesh CreateDiscMesh()
        {
            var vertices = new Vector3[Segments + 1];
            var triangles = new int[Segments * 3];
            vertices[0] = Vector3.zero;

            for (var i = 0; i < Segments; i++)
            {
                var radians = i / (float)Segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i == Segments - 1 ? 1 : i + 2;
            }

            var mesh = new Mesh { name = "Penguin Meteor Target Disc" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Material CreateMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
        }
    }
}
