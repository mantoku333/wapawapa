using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wapawapa.Abilities;

namespace Wapawapa.Gameplay
{
    public sealed class XinZhaoBattleRoyaleController : MonoBehaviour
    {
        private const string GameSceneName = "Game";
        private const string XinZhaoNamePrefix = "Xin Zhao";
        private const string MarchAnimationName = "Spell2";
        private const float ShrinkDuration = 120f;
        private const float FinalRadius = 4f;
        private const float ContactDamage = 30f;
        private const float KnockbackSpeed = 14f;
        private const float HitCooldown = 1.15f;

        private static readonly string[] AttackAnimationNames =
        {
            "Attack1",
            "Attack2",
            "Attack3",
            "Attack",
            "Crit",
            "Spell1",
        };

        private static XinZhaoBattleRoyaleController instance;

        private readonly List<XinZhaoHazardActor> actors = new();
        private readonly Dictionary<int, float> lastHitTimes = new();

        private Vector3 arenaCenter;
        private float elapsed;
        private AudioClip impactSound;
        private bool warnedAboutMissingSpell2;
        private bool warnedAboutMissingAttack;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneCallback()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        public static void ResetRound()
        {
            instance?.ResetToInitialPositions();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != GameSceneName)
            {
                return;
            }

            var xinZhaoRoots = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name.StartsWith(XinZhaoNamePrefix, System.StringComparison.Ordinal))
                {
                    xinZhaoRoots.Add(root);
                }
            }

            if (xinZhaoRoots.Count == 0)
            {
                return;
            }

            var controllerObject = new GameObject(nameof(XinZhaoBattleRoyaleController));
            SceneManager.MoveGameObjectToScene(controllerObject, scene);
            var controller = controllerObject.AddComponent<XinZhaoBattleRoyaleController>();
            controller.Initialize(xinZhaoRoots);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Initialize(IReadOnlyList<GameObject> xinZhaoRoots)
        {
            instance = this;
            arenaCenter = CalculateArenaCenter(xinZhaoRoots);
            var settings = Resources.Load<XinZhaoBattleRoyaleSettings>(nameof(XinZhaoBattleRoyaleSettings));
            impactSound = settings != null ? settings.ImpactSound : null;

            foreach (var root in xinZhaoRoots)
            {
                var actor = root.GetComponent<XinZhaoHazardActor>();
                if (actor == null)
                {
                    actor = root.AddComponent<XinZhaoHazardActor>();
                }

                actor.Configure(this, arenaCenter, impactSound, MarchAnimationName, AttackAnimationNames);
                actors.Add(actor);

                warnedAboutMissingSpell2 |= !actor.HasMarchAnimation;
                warnedAboutMissingAttack |= !actor.HasAttackAnimation;
            }

            if (warnedAboutMissingSpell2)
            {
                Debug.LogWarning(
                    "[XinZhaoBattleRoyale] Spell2 is not included in the current Xin Zhao GLB. " +
                    "Idle1 will be used until a GLB exported with Spell2 is imported.",
                    this);
            }

            if (warnedAboutMissingAttack)
            {
                Debug.LogWarning(
                    "[XinZhaoBattleRoyale] No attack clip was found. Import an export containing Attack1 (or another attack clip).",
                    this);
            }
        }

        private void Update()
        {
            elapsed = Mathf.Min(ShrinkDuration, elapsed + Time.deltaTime);
            var progress = Mathf.Clamp01(elapsed / ShrinkDuration);

            foreach (var actor in actors)
            {
                if (actor != null)
                {
                    actor.SetShrinkProgress(progress, arenaCenter, FinalRadius);
                }
            }
        }

        internal void TryHitPlayer(XinZhaoHazardActor actor, Collider other)
        {
            if (actor == null || other == null)
            {
                return;
            }

            var receiver = other.GetComponentInParent<PlayerDamageReceiver>();
            if (receiver == null || !receiver.IsLocalPlayer || receiver.IsKnockedOut)
            {
                return;
            }

            var receiverId = receiver.GetInstanceID();
            if (lastHitTimes.TryGetValue(receiverId, out var lastHitTime) && Time.time - lastHitTime < HitCooldown)
            {
                return;
            }

            var direction = arenaCenter - receiver.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = actor.transform.forward;
            }
            direction.Normalize();

            var hitPoint = other.ClosestPoint(actor.transform.position);
            var damage = new AbilityDamage(
                "hazard.xinzhao",
                ContactDamage,
                direction,
                KnockbackSpeed,
                hitPoint,
                actor.gameObject);

            if (!AbilityDamageUtility.TryApplyDamage(other, damage))
            {
                return;
            }

            lastHitTimes[receiverId] = Time.time;
            var movement = receiver.GetComponent<DesktopVrNetworkPlayer>();
            if (movement == null)
            {
                movement = receiver.GetComponentInParent<DesktopVrNetworkPlayer>();
            }
            movement?.ApplyExternalImpulse(direction * KnockbackSpeed);

            actor.PlayHitFeedback();
            XinZhaoDamageFlash.Play();
        }

        private void ResetToInitialPositions()
        {
            elapsed = 0f;
            lastHitTimes.Clear();
            foreach (var actor in actors)
            {
                actor?.ResetToInitialPosition();
            }
        }

        private static Vector3 CalculateArenaCenter(IReadOnlyList<GameObject> roots)
        {
            var center = Vector3.zero;
            foreach (var root in roots)
            {
                center += root.transform.position;
            }

            center /= Mathf.Max(1, roots.Count);
            center.y = 0f;
            return center;
        }
    }

    internal sealed class XinZhaoHazardActor : MonoBehaviour
    {
        private XinZhaoBattleRoyaleController controller;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Animation legacyAnimation;
        private string marchAnimationName;
        private string attackAnimationName;
        private AudioSource audioSource;
        private Coroutine attackRoutine;
        private Transform hitboxTransform;

        public bool HasMarchAnimation { get; private set; }
        public bool HasAttackAnimation => !string.IsNullOrEmpty(attackAnimationName);

        public void Configure(
            XinZhaoBattleRoyaleController owner,
            Vector3 arenaCenter,
            AudioClip impactSound,
            string preferredMarchAnimation,
            IReadOnlyList<string> attackAnimations)
        {
            controller = owner;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            legacyAnimation = GetComponentInChildren<Animation>(true);

            marchAnimationName = FindAnimation(preferredMarchAnimation);
            HasMarchAnimation = !string.IsNullOrEmpty(marchAnimationName);
            if (!HasMarchAnimation)
            {
                marchAnimationName = FindAnimation("Idle1");
            }

            foreach (var candidate in attackAnimations)
            {
                attackAnimationName = FindAnimation(candidate);
                if (!string.IsNullOrEmpty(attackAnimationName))
                {
                    break;
                }
            }

            audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.clip = impactSound;
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 20f;
            audioSource.volume = 1f;

            CreateHitbox();
            FaceCenter(arenaCenter);
            PlayMarchAnimation();
        }

        public void SetShrinkProgress(float progress, Vector3 arenaCenter, float finalRadius)
        {
            var radial = initialPosition - arenaCenter;
            radial.y = 0f;
            var initialRadius = radial.magnitude;
            var direction = initialRadius > 0.01f ? radial / initialRadius : Vector3.forward;
            var destinationRadius = Mathf.Min(initialRadius, finalRadius);
            var radius = Mathf.Lerp(initialRadius, destinationRadius, progress);
            transform.position = new Vector3(
                arenaCenter.x + direction.x * radius,
                initialPosition.y,
                arenaCenter.z + direction.z * radius);
            FaceCenter(arenaCenter);
            SyncHitbox();
        }

        public void ResetToInitialPosition()
        {
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }

            transform.SetPositionAndRotation(initialPosition, initialRotation);
            SyncHitbox();
            PlayMarchAnimation();
        }

        public void PlayHitFeedback()
        {
            if (audioSource != null && audioSource.clip != null)
            {
                audioSource.PlayOneShot(audioSource.clip);
            }

            if (legacyAnimation == null || string.IsNullOrEmpty(attackAnimationName))
            {
                return;
            }

            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
            }
            attackRoutine = StartCoroutine(PlayAttackRoutine());
        }

        internal void NotifyTrigger(Collider other)
        {
            controller?.TryHitPlayer(this, other);
        }

        private IEnumerator PlayAttackRoutine()
        {
            var state = legacyAnimation[attackAnimationName];
            if (state == null)
            {
                yield break;
            }

            state.wrapMode = WrapMode.Once;
            state.speed = 1f;
            legacyAnimation.CrossFade(attackAnimationName, 0.06f);
            yield return new WaitForSeconds(Mathf.Max(0.1f, state.length - 0.06f));
            attackRoutine = null;
            PlayMarchAnimation();
        }

        private void PlayMarchAnimation()
        {
            if (legacyAnimation == null || string.IsNullOrEmpty(marchAnimationName))
            {
                return;
            }

            var state = legacyAnimation[marchAnimationName];
            if (state == null)
            {
                return;
            }

            state.wrapMode = WrapMode.Loop;
            state.speed = 1f;
            legacyAnimation.CrossFade(marchAnimationName, 0.1f);
        }

        private string FindAnimation(string requestedName)
        {
            if (legacyAnimation == null)
            {
                return null;
            }

            foreach (AnimationState state in legacyAnimation)
            {
                if (string.Equals(state.name, requestedName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return state.name;
                }
            }

            return null;
        }

        private void CreateHitbox()
        {
            var hitbox = new GameObject($"{name} Hazard Trigger");
            hitbox.transform.SetParent(controller.transform, false);
            hitboxTransform = hitbox.transform;

            var capsule = hitbox.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.direction = 1;
            capsule.radius = 0.6f;
            capsule.height = 2.3f;

            var body = hitbox.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var relay = hitbox.AddComponent<XinZhaoHazardTrigger>();
            relay.Initialize(this);
            SyncHitbox();
        }

        private void SyncHitbox()
        {
            if (hitboxTransform != null)
            {
                hitboxTransform.position = transform.position + Vector3.up * 1.15f;
                hitboxTransform.rotation = Quaternion.identity;
            }
        }

        private void FaceCenter(Vector3 arenaCenter)
        {
            var direction = arenaCenter - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }
    }

    internal sealed class XinZhaoHazardTrigger : MonoBehaviour
    {
        private XinZhaoHazardActor owner;

        public void Initialize(XinZhaoHazardActor actor)
        {
            owner = actor;
        }

        private void OnTriggerEnter(Collider other)
        {
            owner?.NotifyTrigger(other);
        }

        private void OnTriggerStay(Collider other)
        {
            owner?.NotifyTrigger(other);
        }
    }

    internal sealed class XinZhaoDamageFlash : MonoBehaviour
    {
        private static XinZhaoDamageFlash instance;

        private Image overlay;
        private Coroutine flashRoutine;

        public static void Play()
        {
            Ensure().StartFlash();
        }

        private static XinZhaoDamageFlash Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            var root = new GameObject(nameof(XinZhaoDamageFlash));
            instance = root.AddComponent<XinZhaoDamageFlash>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            CreateOverlay();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void CreateOverlay()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            overlay = new GameObject("Damage Flash").AddComponent<Image>();
            overlay.transform.SetParent(transform, false);
            overlay.raycastTarget = false;
            overlay.color = new Color(1f, 0.04f, 0.04f, 0f);
            var rect = overlay.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void StartFlash()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            const float peakAlpha = 0.28f;
            const float holdDuration = 0.06f;
            const float fadeDuration = 0.24f;

            overlay.color = new Color(1f, 0.04f, 0.04f, peakAlpha);
            yield return new WaitForSecondsRealtime(holdDuration);

            var elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var alpha = Mathf.Lerp(peakAlpha, 0f, elapsed / fadeDuration);
                overlay.color = new Color(1f, 0.04f, 0.04f, alpha);
                yield return null;
            }

            overlay.color = new Color(1f, 0.04f, 0.04f, 0f);
            flashRoutine = null;
        }
    }
}
