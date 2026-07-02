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

        [Header("Wave")]
        [SerializeField] private float speed = 7.5f;
        [SerializeField] private float lifetime = 4f;
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

        private Transform rightHand;
        private Transform fallbackAim;
        private GameObject owner;
        private RedirectWaveProjectile activeWave;
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
            }

            if (sampledPunchSpeed >= minimumPunchSpeed)
            {
                ConsumePunch(sampledPunchDirection);
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
                Debug.Log("Redirect Wave input window expired.");
                return;
            }

            if (sampledPunchSpeed >= minimumPunchSpeed)
            {
                ConsumePunch(sampledPunchDirection);
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

        private void ConsumePunch(Vector3 direction)
        {
            waitingForPunch = false;
            sampledPunchSpeed = 0f;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = fallbackAim != null ? fallbackAim.forward : transform.forward;
            }

            if (activeWave == null)
            {
                SpawnWave(direction.normalized);
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
                : fallbackAim.position + direction * 0.6f;

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
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            renderer.material = new Material(shader);

            var trail = waveObject.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.startWidth = radius * 1.5f;
            trail.endWidth = 0f;
            trail.material = new Material(shader);

            activeWave = waveObject.AddComponent<RedirectWaveProjectile>();
            activeWave.Initialize(
                AbilityId,
                owner,
                direction,
                speed,
                lifetime,
                initialDamage,
                redirectedDamage,
                initialPushForce,
                redirectedPushForce,
                hitMask,
                renderer,
                trail,
                initialColor,
                redirectedColor,
                HandleWaveDestroyed);
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
        private float initialDamage;
        private float redirectedDamage;
        private float initialPushForce;
        private float redirectedPushForce;
        private LayerMask hitMask;
        private Renderer waveRenderer;
        private TrailRenderer trail;
        private Color redirectedColor;
        private Action<RedirectWaveProjectile> onDestroyed;
        private bool redirected;

        public void Initialize(
            string abilityId,
            GameObject owner,
            Vector3 direction,
            float speed,
            float lifetime,
            float initialDamage,
            float redirectedDamage,
            float initialPushForce,
            float redirectedPushForce,
            LayerMask hitMask,
            Renderer waveRenderer,
            TrailRenderer trail,
            Color initialColor,
            Color redirectedColor,
            Action<RedirectWaveProjectile> onDestroyed)
        {
            this.abilityId = abilityId;
            this.owner = owner;
            this.direction = direction;
            this.speed = Mathf.Max(0.1f, speed);
            destroyTime = Time.time + Mathf.Max(0.1f, lifetime);
            this.initialDamage = initialDamage;
            this.redirectedDamage = redirectedDamage;
            this.initialPushForce = initialPushForce;
            this.redirectedPushForce = redirectedPushForce;
            this.hitMask = hitMask;
            this.waveRenderer = waveRenderer;
            this.trail = trail;
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
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            SetColor(redirectedColor);
            Debug.Log("Redirect Wave changed direction.");
            return true;
        }

        private void FixedUpdate()
        {
            transform.position += direction * (speed * Time.fixedDeltaTime);

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
                waveRenderer.material.color = color;
            }

            if (trail != null)
            {
                trail.startColor = color;
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
            }
        }

        private void OnDestroy()
        {
            onDestroyed?.Invoke(this);
        }
    }
}
