using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

namespace Wapawapa.Abilities
{
    public sealed class BloodFocusStrikeAbility : AbilityBase
    {
        public const string BlackFlashAbilityId = "kita.blood_focus_strike";

        [Header("Shot Gauge")]
        [SerializeField] private float gaugeCycleSeconds = 1.4f;
        [SerializeField, Range(0.5f, 0.99f)] private float maxZoneStartRatio = 0.9f;

        [Header("Strike")]
        [SerializeField] private float baseDamage = 18f;
        [SerializeField] private float focusedDamageMultiplier = 2f;
        [SerializeField] private float pushForce = 9f;
        [SerializeField] private float hitRadius = 0.45f;
        [SerializeField] private float minimumHandSpeed = 0.9f;
        [SerializeField] private LayerMask hitMask = ~0;

        [Header("UI")]
        [SerializeField] private bool showGaugeUi = true;
        [SerializeField] private Vector2 gaugeAnchoredPosition = new Vector2(-88f, 0f);
        [SerializeField] private Vector2 gaugeSize = new Vector2(34f, 260f);

        [Header("Visuals")]
        [SerializeField] private bool spawnDebugVisuals = true;
        [SerializeField] private float focusVisualScale = 0.5f;
        [SerializeField] private float impactEffectLifetime = 0.35f;
        [SerializeField] private float impactEffectScale = 1.2f;

        [Header("Sound")]
        [SerializeField] private AudioClip blackFlashClip;
        [SerializeField] private AudioClip blackFlashLightningClip;
        [SerializeField] private float blackFlashVolume = 1f;
        [SerializeField] private float generatedSoundDuration = 0.28f;

        private float gaugeRatio;
        private int gaugeCycleIndex;
        private int lastBlackFlashCycleIndex = -1;
        private GameObject currentOwner;
        private Transform leftHand;
        private Transform rightHand;
        private Vector3 previousLeftPosition;
        private Vector3 previousRightPosition;
        private GameObject leftFocusVisual;
        private GameObject rightFocusVisual;
        private Material focusMaterial;
        private Canvas gaugeCanvas;
        private RectTransform gaugeFillRect;
        private Image gaugeFillImage;
        private Image maxZoneImage;
        private AudioClip generatedBlackFlashClip;
        private static AudioClip sharedGeneratedBlackFlashClip;
        private NetworkObject networkObject;

        protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
        {
            CacheContext(context);
        }

        private void OnEnable()
        {
            networkObject = GetComponentInParent<NetworkObject>();
            currentOwner = gameObject;
            DiscoverHandReferences();
            UpdatePreviousHandPositions();

            if (showGaugeUi && CanDriveLocalAbility())
            {
                CreateGaugeUi();
            }
        }

        private void Update()
        {
            if (!CanDriveLocalAbility())
            {
                DestroyGaugeUi();
                return;
            }

            UpdateGauge();
            UpdateGaugeUi();
            UpdateBlackFlashStrike();
        }

        private bool CanDriveLocalAbility()
        {
            return networkObject == null || !networkObject.IsValid || networkObject.HasStateAuthority;
        }

        private void UpdateGauge()
        {
            var cycle = Mathf.Max(gaugeCycleSeconds, 0.0001f);
            var cycleProgress = Time.time / cycle;
            gaugeCycleIndex = Mathf.FloorToInt(cycleProgress);
            gaugeRatio = Mathf.Repeat(cycleProgress, 1f);
        }

        private void UpdateGaugeUi()
        {
            if (!showGaugeUi)
            {
                DestroyGaugeUi();
                return;
            }

            if (gaugeCanvas == null)
            {
                CreateGaugeUi();
            }

            if (gaugeFillRect == null || gaugeFillImage == null)
            {
                return;
            }

            gaugeFillRect.anchorMax = new Vector2(1f, gaugeRatio);
            gaugeFillImage.color = IsGaugeInMaxZone
                ? new Color(1f, 0.06f, 0.02f, 0.98f)
                : new Color(1f, 0.58f, 0.06f, 0.92f);

            if (maxZoneImage != null)
            {
                maxZoneImage.rectTransform.anchorMin = new Vector2(0f, maxZoneStartRatio);
                maxZoneImage.rectTransform.anchorMax = Vector2.one;
                maxZoneImage.color = IsGaugeInMaxZone
                    ? new Color(1f, 0.02f, 0.02f, 0.44f)
                    : new Color(1f, 0.1f, 0.04f, 0.24f);
            }
        }

        private void UpdateBlackFlashStrike()
        {
            if (currentOwner == null)
            {
                currentOwner = gameObject;
            }

            if (leftHand == null || rightHand == null)
            {
                DiscoverHandReferences();
            }

            if (TryApplyBlackFlashStrike(leftHand, ref previousLeftPosition) ||
                TryApplyBlackFlashStrike(rightHand, ref previousRightPosition))
            {
                lastBlackFlashCycleIndex = gaugeCycleIndex;
            }
        }

        private bool TryApplyBlackFlashStrike(Transform hand, ref Vector3 previousPosition)
        {
            if (hand == null)
            {
                return false;
            }

            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            var currentPosition = hand.position;
            var velocity = (currentPosition - previousPosition) / deltaTime;
            previousPosition = currentPosition;

            if (!IsGaugeInMaxZone || lastBlackFlashCycleIndex == gaugeCycleIndex)
            {
                return false;
            }

            if (velocity.magnitude < minimumHandSpeed)
            {
                return false;
            }

            var hits = Physics.OverlapSphere(currentPosition, hitRadius, hitMask, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }

                if (currentOwner != null && hit.transform.IsChildOf(currentOwner.transform))
                {
                    continue;
                }

                var hitPoint = hit.ClosestPoint(currentPosition);
                var damage = new AbilityDamage(
                    AbilityId,
                    baseDamage * focusedDamageMultiplier,
                    velocity.normalized,
                    pushForce,
                    hitPoint,
                    currentOwner);

                if (AbilityDamageUtility.TryApplyDamage(hit, damage))
                {
                    if (!IsNetworkedActive)
                    {
                        PlayBlackFlashSound(hitPoint);
                        SpawnBlackFlashEffect(hitPoint, velocity.normalized);
                    }

                    EnsureFocusVisuals();
                    Invoke(nameof(ClearFocus), 0.2f);
                    return true;
                }
            }

            return false;
        }

        private bool IsGaugeInMaxZone => gaugeRatio >= maxZoneStartRatio;
        private bool IsNetworkedActive => networkObject != null && networkObject.IsValid;

        private void CacheContext(in AbilityContext context)
        {
            currentOwner = context.Owner;
            leftHand = context.LeftHand;
            rightHand = context.RightHand;
        }

        private void DiscoverHandReferences()
        {
            if (leftHand == null)
            {
                leftHand = FindChildByName(transform, "LeftHand");
            }

            if (rightHand == null)
            {
                rightHand = FindChildByName(transform, "RightHand");
            }
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private void UpdatePreviousHandPositions()
        {
            if (leftHand != null)
            {
                previousLeftPosition = leftHand.position;
            }

            if (rightHand != null)
            {
                previousRightPosition = rightHand.position;
            }
        }

        private void CreateGaugeUi()
        {
            if (gaugeCanvas != null)
            {
                return;
            }

            var canvasObject = new GameObject("Blood Focus Gauge UI");
            canvasObject.transform.SetParent(transform, false);
            gaugeCanvas = canvasObject.AddComponent<Canvas>();
            gaugeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gaugeCanvas.sortingOrder = 650;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var root = new GameObject("Gauge").AddComponent<RectTransform>();
            root.SetParent(gaugeCanvas.transform, false);
            root.anchorMin = new Vector2(1f, 0.5f);
            root.anchorMax = new Vector2(1f, 0.5f);
            root.pivot = new Vector2(1f, 0.5f);
            root.anchoredPosition = gaugeAnchoredPosition;
            root.sizeDelta = gaugeSize;

            var background = CreateImage("Background", root, new Color(0.03f, 0.03f, 0.035f, 0.78f));
            Stretch(background.rectTransform);

            maxZoneImage = CreateImage("Max Zone", root, new Color(1f, 0.1f, 0.04f, 0.24f));
            maxZoneImage.raycastTarget = false;
            maxZoneImage.rectTransform.anchorMin = new Vector2(0f, maxZoneStartRatio);
            maxZoneImage.rectTransform.anchorMax = Vector2.one;
            maxZoneImage.rectTransform.offsetMin = Vector2.zero;
            maxZoneImage.rectTransform.offsetMax = Vector2.zero;

            var fill = CreateImage("Fill", root, new Color(1f, 0.58f, 0.06f, 0.92f));
            gaugeFillImage = fill;
            gaugeFillImage.raycastTarget = false;
            gaugeFillRect = fill.rectTransform;
            gaugeFillRect.anchorMin = Vector2.zero;
            gaugeFillRect.anchorMax = new Vector2(1f, 0f);
            gaugeFillRect.offsetMin = Vector2.zero;
            gaugeFillRect.offsetMax = Vector2.zero;

            CreateBorder(root, new Color(1f, 1f, 1f, 0.82f), 3f);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void CreateBorder(Transform parent, Color color, float thickness)
        {
            CreateBorderLine("Left", parent, color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f));
            CreateBorderLine("Right", parent, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f));
            CreateBorderLine("Top", parent, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness));
            CreateBorderLine("Bottom", parent, color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness));
        }

        private static void CreateBorderLine(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta)
        {
            var line = CreateImage($"Frame {name}", parent, color);
            line.raycastTarget = false;
            var rect = line.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = sizeDelta;
            rect.anchoredPosition = Vector2.zero;
        }

        private void EnsureFocusVisuals()
        {
            if (!spawnDebugVisuals)
            {
                return;
            }

            if (focusMaterial == null)
            {
                focusMaterial = CreateVisualMaterial(new Color(1f, 0.02f, 0.01f, 0.65f));
            }

            leftFocusVisual = AttachFocusVisual(leftHand, leftFocusVisual);
            rightFocusVisual = AttachFocusVisual(rightHand, rightFocusVisual);
        }

        private GameObject AttachFocusVisual(Transform hand, GameObject existing)
        {
            if (hand == null)
            {
                return existing;
            }

            var visual = existing != null ? existing : CreateVisualSphere("Blood Focus Fist", focusMaterial);
            visual.transform.SetParent(hand, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * focusVisualScale;
            return visual;
        }

        private void ClearFocus()
        {
            DestroyFocusVisuals();
        }

        private void SpawnMissedTimingPulse(Vector3 position)
        {
            if (!spawnDebugVisuals)
            {
                return;
            }

            var pulse = CreateVisualSphere("Blood Focus Missed Pulse", CreateVisualMaterial(new Color(0.3f, 0.3f, 0.3f, 0.35f)));
            pulse.transform.position = position;
            pulse.transform.localScale = Vector3.one * (hitRadius * 1.5f);
            Destroy(pulse, 0.25f);
        }

        private void PlayBlackFlashSound(Vector3 position)
        {
            PlayerCombatAudio.PlayBlackFlashImpact(position, blackFlashClip, blackFlashLightningClip, blackFlashVolume);
        }

        private AudioClip GetGeneratedBlackFlashClip()
        {
            if (generatedBlackFlashClip != null)
            {
                return generatedBlackFlashClip;
            }

            var sampleRate = 44100;
            var duration = Mathf.Max(0.08f, generatedSoundDuration);
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var normalizedTime = t / duration;
                var punchEnvelope = Mathf.Exp(-normalizedTime * 10f);
                var crackEnvelope = Mathf.Exp(-normalizedTime * 22f);
                var lowHit = Mathf.Sin(2f * Mathf.PI * 72f * t) * punchEnvelope * 0.75f;
                var highCrack = Mathf.Sin(2f * Mathf.PI * 1480f * t) * crackEnvelope * 0.22f;
                var grit = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 620f * t)) * crackEnvelope * 0.18f;
                samples[i] = Mathf.Clamp(lowHit + highCrack + grit, -1f, 1f);
            }

            generatedBlackFlashClip = AudioClip.Create("Generated Black Flash", sampleCount, 1, sampleRate, false);
            generatedBlackFlashClip.SetData(samples, 0);
            return generatedBlackFlashClip;
        }

        public static void PlayNetworkFeedback(Vector3 position, Vector3 direction)
        {
            PlayerCombatAudio.PlayBlackFlashImpact(position);
            SpawnSharedBlackFlashEffect(position, direction);
        }

        private static AudioClip GetSharedGeneratedBlackFlashClip()
        {
            if (sharedGeneratedBlackFlashClip != null)
            {
                return sharedGeneratedBlackFlashClip;
            }

            var sampleRate = 44100;
            var duration = 0.28f;
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var normalizedTime = t / duration;
                var punchEnvelope = Mathf.Exp(-normalizedTime * 10f);
                var crackEnvelope = Mathf.Exp(-normalizedTime * 22f);
                var lowHit = Mathf.Sin(2f * Mathf.PI * 72f * t) * punchEnvelope * 0.75f;
                var highCrack = Mathf.Sin(2f * Mathf.PI * 1480f * t) * crackEnvelope * 0.22f;
                var grit = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 620f * t)) * crackEnvelope * 0.18f;
                samples[i] = Mathf.Clamp(lowHit + highCrack + grit, -1f, 1f);
            }

            sharedGeneratedBlackFlashClip = AudioClip.Create("Generated Network Black Flash", sampleCount, 1, sampleRate, false);
            sharedGeneratedBlackFlashClip.SetData(samples, 0);
            return sharedGeneratedBlackFlashClip;
        }

        private static void SpawnSharedBlackFlashEffect(Vector3 position, Vector3 direction)
        {
            var root = new GameObject("Black Flash Network Impact");
            root.transform.position = position;
            Destroy(root, 0.45f);

            var blackMaterial = CreateVisualMaterial(new Color(0.01f, 0f, 0.005f, 0.95f));
            var redMaterial = CreateVisualMaterial(new Color(1f, 0f, 0.02f, 0.85f));
            var emberMaterial = CreateVisualMaterial(new Color(1f, 0.18f, 0.02f, 0.8f));

            var core = CreateVisualSphere("Black Flash Network Core", blackMaterial);
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * 0.55f;

            var ring = CreateVisualSphere("Black Flash Network Ring", redMaterial);
            ring.transform.SetParent(root.transform, false);
            ring.transform.localScale = new Vector3(1.6f, 0.04f, 1.6f);

            var forward = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            var rotation = Quaternion.LookRotation(forward, Vector3.up);
            for (var i = 0; i < 8; i++)
            {
                var sparkDirection = rotation * Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                var spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spark.name = "Black Flash Network Spark";
                spark.transform.SetParent(root.transform, false);
                spark.transform.localPosition = sparkDirection * 0.45f;
                spark.transform.localRotation = Quaternion.LookRotation(sparkDirection, Vector3.up);
                spark.transform.localScale = new Vector3(0.04f, 0.04f, 0.8f);

                if (spark.TryGetComponent(out Collider collider))
                {
                    Destroy(collider);
                }

                if (spark.TryGetComponent(out Renderer renderer))
                {
                    renderer.sharedMaterial = emberMaterial;
                }
            }
        }

        private void SpawnBlackFlashEffect(Vector3 position, Vector3 direction)
        {
            if (!spawnDebugVisuals)
            {
                return;
            }

            var root = new GameObject("Black Flash Impact");
            root.transform.position = position;
            Destroy(root, impactEffectLifetime + 0.1f);

            var redMaterial = CreateVisualMaterial(new Color(1f, 0f, 0.02f, 0.9f));
            var blackMaterial = CreateVisualMaterial(new Color(0.01f, 0f, 0.005f, 0.95f));
            var emberMaterial = CreateVisualMaterial(new Color(1f, 0.18f, 0.02f, 0.85f));

            var core = CreateVisualSphere("Black Flash Core", blackMaterial);
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * hitRadius * impactEffectScale;
            StartCoroutine(AnimatePulse(core.transform, Vector3.one * hitRadius * 0.35f, Vector3.one * hitRadius * impactEffectScale, impactEffectLifetime, blackMaterial));

            var ring = CreateVisualSphere("Black Flash Ring", redMaterial);
            ring.transform.SetParent(root.transform, false);
            ring.transform.localScale = new Vector3(hitRadius * 0.5f, hitRadius * 0.08f, hitRadius * 0.5f);
            StartCoroutine(AnimatePulse(
                ring.transform,
                new Vector3(hitRadius * 0.4f, hitRadius * 0.06f, hitRadius * 0.4f),
                new Vector3(hitRadius * 4.2f, hitRadius * 0.06f, hitRadius * 4.2f),
                impactEffectLifetime,
                redMaterial));

            var sparkForward = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
            var rotation = Quaternion.LookRotation(sparkForward, Vector3.up);
            for (var i = 0; i < 10; i++)
            {
                var angle = i * 36f;
                var sparkDirection = rotation * Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                var spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spark.name = "Black Flash Spark";
                spark.transform.SetParent(root.transform, false);
                spark.transform.localPosition = Vector3.zero;
                spark.transform.localRotation = Quaternion.LookRotation(sparkDirection, Vector3.up);
                spark.transform.localScale = new Vector3(0.04f, 0.04f, hitRadius * 1.6f);

                if (spark.TryGetComponent(out Collider collider))
                {
                    Destroy(collider);
                }

                if (spark.TryGetComponent(out Renderer renderer))
                {
                    renderer.sharedMaterial = emberMaterial;
                }

                StartCoroutine(AnimateShard(spark.transform, sparkDirection, hitRadius * 3.5f, impactEffectLifetime, emberMaterial));
            }
        }

        private IEnumerator AnimatePulse(Transform target, Vector3 startScale, Vector3 endScale, float duration, Material material)
        {
            var lifetime = Mathf.Max(0.01f, duration);
            var startedAt = Time.time;
            var baseColor = material != null ? material.color : Color.white;

            while (target != null)
            {
                var ratio = Mathf.Clamp01((Time.time - startedAt) / lifetime);
                target.localScale = Vector3.Lerp(startScale, endScale, ratio);

                if (material != null)
                {
                    var color = baseColor;
                    color.a *= 1f - ratio;
                    material.color = color;
                }

                if (ratio >= 1f)
                {
                    Destroy(target.gameObject);
                    yield break;
                }

                yield return null;
            }
        }

        private IEnumerator AnimateShard(Transform target, Vector3 direction, float distance, float duration, Material material)
        {
            var lifetime = Mathf.Max(0.01f, duration);
            var startedAt = Time.time;
            var startScale = target != null ? target.localScale : Vector3.one;
            var moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            var baseColor = material != null ? material.color : Color.white;

            while (target != null)
            {
                var ratio = Mathf.Clamp01((Time.time - startedAt) / lifetime);
                target.localPosition = moveDirection * distance * ratio;
                target.localScale = Vector3.Lerp(startScale, Vector3.zero, ratio);

                if (material != null)
                {
                    var color = baseColor;
                    color.a *= 1f - ratio;
                    material.color = color;
                }

                if (ratio >= 1f)
                {
                    Destroy(target.gameObject);
                    yield break;
                }

                yield return null;
            }
        }

        private static GameObject CreateVisualSphere(string objectName, Material material)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = objectName;

            if (visual.TryGetComponent(out Collider collider))
            {
                Destroy(collider);
            }

            if (visual.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
            }

            return visual;
        }

        private static Material CreateVisualMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            return new Material(shader)
            {
                color = color
            };
        }

        private void DestroyGaugeUi()
        {
            if (gaugeCanvas != null)
            {
                Destroy(gaugeCanvas.gameObject);
                gaugeCanvas = null;
                gaugeFillRect = null;
                gaugeFillImage = null;
                maxZoneImage = null;
            }
        }

        private void DestroyFocusVisuals()
        {
            if (leftFocusVisual != null)
            {
                Destroy(leftFocusVisual);
                leftFocusVisual = null;
            }

            if (rightFocusVisual != null)
            {
                Destroy(rightFocusVisual);
                rightFocusVisual = null;
            }
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(ClearFocus));
            DestroyGaugeUi();
            DestroyFocusVisuals();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
            if (leftHand != null)
            {
                Gizmos.DrawWireSphere(leftHand.position, hitRadius);
            }

            if (rightHand != null)
            {
                Gizmos.DrawWireSphere(rightHand.position, hitRadius);
            }
        }

    }
}
