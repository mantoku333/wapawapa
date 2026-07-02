using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wapawapa.Abilities
{
    public sealed class RedirectWaveAbility : AbilityBase
    {
        [Header("Punch Detection")]
        [SerializeField] private float punchInputWindow = 1.2f;
        [SerializeField] private float minimumPunchSpeed = 0.8f;
        [SerializeField] private float minimumPunchDistance = 0.18f;

        [Header("Wave")]
        [SerializeField] private float speed = 7.5f;
        [SerializeField] private float lifetime = 8f;
        [SerializeField] private float firstTravelDistance = 3.2f;
        [SerializeField] private float redirectedTravelDistance = 3.2f;
        [SerializeField] private float radius = 0.28f;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("Damage")]
        [SerializeField] private float initialDamage = 8f;
        [SerializeField] private float redirectedDamage = 14f;
        [SerializeField] private float initialPushForce = 3f;
        [SerializeField] private float redirectedPushForce = 6f;

        [Header("Appearance")]
        [SerializeField] private Color initialColor = new Color(0.15f, 0.85f, 1f, 0.9f);
        [SerializeField] private Color redirectedColor = new Color(1f, 0.2f, 0.75f, 0.95f);
        [SerializeField] private Color armedColor = new Color(1f, 0.85f, 0.2f, 0.8f);

        private Transform rightHand;
        private Transform fallbackAim;
        private GameObject owner;
        private RedirectWaveProjectile activeWave;
        private GameObject armedMarker;
        private Vector3 armedHandLocalPosition;
        private Vector3 previousHandLocalPosition;
        private Vector3 sampledPunchDirection;
        private float sampledPunchSpeed;
        private float inputWindowEndsAt;
        private bool waitingForPunch;

        protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
        {
            owner = context.Owner;
            rightHand = context.RightHand;
            fallbackAim = context.AimSource;
            inputWindowEndsAt = Time.time + punchInputWindow;
            waitingForPunch = true;

            if (rightHand != null)
            {
                previousHandLocalPosition = rightHand.localPosition;
                armedHandLocalPosition = rightHand.localPosition;
            }

            ShowArmedMarker();

            if (TryGetCurrentPunchDirection(out var punchDirection))
            {
                ConsumePunch(punchDirection);
            }
            else
            {
                Debug.Log(activeWave == null
                    ? "Redirect Wave armed. Punch with the right hand."
                    : "Redirect armed. Punch with the right hand to turn the wave.");
            }
        }

        private void Update()
        {
            SampleRightHandVelocity();

            if (!waitingForPunch)
            {
                return;
            }

            if (Time.time > inputWindowEndsAt)
            {
                waitingForPunch = false;
                HideArmedMarker();
                Debug.Log("Redirect Wave input window expired.");
                return;
            }

            if (TryGetCurrentPunchDirection(out var punchDirection))
            {
                ConsumePunch(punchDirection);
            }
        }

        private void SampleRightHandVelocity()
        {
            if (rightHand == null || Time.deltaTime <= 0f)
            {
                sampledPunchSpeed = 0f;
                return;
            }

            var localDelta = rightHand.localPosition - previousHandLocalPosition;
            previousHandLocalPosition = rightHand.localPosition;
            var parent = rightHand.parent;
            var worldDelta = parent != null ? parent.TransformVector(localDelta) : localDelta;
            sampledPunchSpeed = worldDelta.magnitude / Time.deltaTime;

            if (worldDelta.sqrMagnitude > 0.000001f)
            {
                sampledPunchDirection = worldDelta.normalized;
            }
        }

        private bool TryGetArmedPunchDirection(out Vector3 direction)
        {
            direction = Vector3.zero;

            if (rightHand == null)
            {
                return false;
            }

            var localDelta = rightHand.localPosition - armedHandLocalPosition;
            if (localDelta.magnitude < minimumPunchDistance)
            {
                return false;
            }

            var parent = rightHand.parent;
            var worldDelta = parent != null ? parent.TransformVector(localDelta) : localDelta;
            if (worldDelta.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            direction = worldDelta.normalized;
            return true;
        }

        private bool TryGetCurrentPunchDirection(out Vector3 direction)
        {
            if (sampledPunchSpeed >= minimumPunchSpeed)
            {
                direction = ResolveForwardSafePunchDirection(sampledPunchDirection);
                return true;
            }

            if (TryGetArmedPunchDirection(out direction))
            {
                direction = ResolveForwardSafePunchDirection(direction);
                return true;
            }

            return false;
        }

        private Vector3 ResolveForwardSafePunchDirection(Vector3 direction)
        {
            var forward = fallbackAim != null ? fallbackAim.forward : transform.forward;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return forward;
            }

            direction = direction.normalized;
            return Vector3.Dot(direction, forward) < 0f ? forward : direction;
        }

        private void ConsumePunch(Vector3 direction)
        {
            waitingForPunch = false;
            HideArmedMarker();
            sampledPunchSpeed = 0f;

            direction = ResolveForwardSafePunchDirection(direction);

            if (activeWave == null)
            {
                SpawnWave(direction.normalized);
                Debug.Log("Redirect Wave spawned.");
                return;
            }

            if (!activeWave.TryRedirect(direction.normalized))
            {
                Debug.Log("This wave has already been redirected once.");
            }
        }

        private void SpawnWave(Vector3 direction)
        {
            var origin = rightHand != null
                ? rightHand.position + direction * 0.35f
                : fallbackAim != null
                    ? fallbackAim.position + direction * 0.6f
                    : transform.position + direction * 0.6f;

            var waveObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            waveObject.name = "Redirect Wave";
            waveObject.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction, Vector3.up));
            waveObject.transform.localScale = Vector3.one * (radius * 2f);

            var sphereCollider = waveObject.GetComponent<SphereCollider>();
            sphereCollider.isTrigger = true;

            var rigidbody = waveObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var renderer = waveObject.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default");
            renderer.material = new Material(shader);

            var trail = waveObject.AddComponent<TrailRenderer>();
            trail.time = 0.75f;
            trail.startWidth = radius * 2.2f;
            trail.endWidth = 0f;
            trail.material = new Material(shader);

            var light = waveObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3f;
            light.intensity = 2.5f;
            light.color = initialColor;

            activeWave = waveObject.AddComponent<RedirectWaveProjectile>();
            activeWave.Initialize(
                AbilityId,
                owner,
                direction,
                speed,
                lifetime,
                firstTravelDistance,
                redirectedTravelDistance,
                initialDamage,
                redirectedDamage,
                initialPushForce,
                redirectedPushForce,
                hitMask,
                renderer,
                trail,
                light,
                initialColor,
                redirectedColor,
                HandleWaveDestroyed);
        }

        private void ShowArmedMarker()
        {
            HideArmedMarker();

            var markerPosition = rightHand != null
                ? rightHand.position
                : fallbackAim != null
                    ? fallbackAim.position + fallbackAim.forward * 0.5f
                    : transform.position + transform.forward * 0.5f;

            armedMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            armedMarker.name = "Redirect Wave Armed Marker";
            armedMarker.transform.position = markerPosition;
            armedMarker.transform.localScale = Vector3.one * 0.18f;

            var collider = armedMarker.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = armedMarker.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Sprites/Default");
            renderer.material = new Material(shader);
            SetMaterialColor(renderer.material, armedColor);

            var light = armedMarker.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 1.25f;
            light.intensity = 1.5f;
            light.color = armedColor;

            Destroy(armedMarker, punchInputWindow);
        }

        private void HideArmedMarker()
        {
            if (armedMarker != null)
            {
                Destroy(armedMarker);
                armedMarker = null;
            }
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

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
            }
        }

        private void HandleWaveDestroyed(RedirectWaveProjectile wave)
        {
            if (activeWave == wave)
            {
                activeWave = null;
            }
        }
    }

    internal sealed class RedirectWaveProjectile : MonoBehaviour
    {
        private readonly HashSet<Transform> damagedRoots = new HashSet<Transform>();

        private string abilityId;
        private GameObject owner;
        private Vector3 direction;
        private float speed;
        private float destroyTime;
        private float remainingTravelDistance;
        private float redirectedTravelDistance;
        private float initialDamage;
        private float redirectedDamage;
        private float initialPushForce;
        private float redirectedPushForce;
        private LayerMask hitMask;
        private Renderer waveRenderer;
        private TrailRenderer trail;
        private Light waveLight;
        private Color redirectedColor;
        private Action<RedirectWaveProjectile> onDestroyed;
        private bool redirected;

        public void Initialize(
            string abilityId,
            GameObject owner,
            Vector3 direction,
            float speed,
            float lifetime,
            float firstTravelDistance,
            float redirectedTravelDistance,
            float initialDamage,
            float redirectedDamage,
            float initialPushForce,
            float redirectedPushForce,
            LayerMask hitMask,
            Renderer waveRenderer,
            TrailRenderer trail,
            Light waveLight,
            Color initialColor,
            Color redirectedColor,
            Action<RedirectWaveProjectile> onDestroyed)
        {
            this.abilityId = abilityId;
            this.owner = owner;
            this.direction = direction;
            this.speed = Mathf.Max(0.1f, speed);
            destroyTime = Time.time + Mathf.Max(0.1f, lifetime);
            remainingTravelDistance = Mathf.Max(0.1f, firstTravelDistance);
            this.redirectedTravelDistance = Mathf.Max(0.1f, redirectedTravelDistance);
            this.initialDamage = initialDamage;
            this.redirectedDamage = redirectedDamage;
            this.initialPushForce = initialPushForce;
            this.redirectedPushForce = redirectedPushForce;
            this.hitMask = hitMask;
            this.waveRenderer = waveRenderer;
            this.trail = trail;
            this.waveLight = waveLight;
            this.redirectedColor = redirectedColor;
            this.onDestroyed = onDestroyed;
            SetColor(initialColor);
        }

        public bool TryRedirect(Vector3 newDirection)
        {
            if (redirected || newDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            redirected = true;
            direction = newDirection.normalized;
            remainingTravelDistance = redirectedTravelDistance;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            SetColor(redirectedColor);
            Debug.Log("Redirect Wave changed direction.");
            return true;
        }

        private void FixedUpdate()
        {
            if (remainingTravelDistance > 0f)
            {
                var moveDistance = Mathf.Min(speed * Time.fixedDeltaTime, remainingTravelDistance);
                transform.position += direction * moveDistance;
                remainingTravelDistance -= moveDistance;

                if (redirected && remainingTravelDistance <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            if (Time.time >= destroyTime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & hitMask.value) == 0)
            {
                return;
            }

            if (owner != null && other.transform.IsChildOf(owner.transform))
            {
                return;
            }

            var root = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform.root;
            if (root == null || !damagedRoots.Add(root))
            {
                return;
            }

            var damage = redirected ? redirectedDamage : initialDamage;
            var pushForce = redirected ? redirectedPushForce : initialPushForce;
            var hitPoint = other.ClosestPoint(transform.position);
            var damageData = new AbilityDamage(abilityId, damage, direction, pushForce, hitPoint, owner);

            if (AbilityDamageUtility.TryApplyDamage(other, damageData))
            {
                Destroy(gameObject);
            }
        }

        private void SetColor(Color color)
        {
            if (waveRenderer != null)
            {
                SetProjectileMaterialColor(waveRenderer.material, color);
            }

            if (trail != null)
            {
                trail.startColor = color;
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
                SetProjectileMaterialColor(trail.material, color);
            }

            if (waveLight != null)
            {
                waveLight.color = color;
            }
        }

        private static void SetProjectileMaterialColor(Material material, Color color)
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

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
            }
        }

        private void OnDestroy()
        {
            onDestroyed?.Invoke(this);
        }
    }
}
