using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Video;

namespace Wapawapa.Abilities
{
    public sealed class FlameBombAbility : AbilityBase
    {
        private enum FlameBombState
        {
            Waiting,
            Charging,
            Flying
        }

        [Header("右手参照")]
        [Tooltip("プレイヤー本体です。未設定の場合はこのコンポーネントのGameObjectを使います。")]
        [SerializeField] private GameObject 所有者;

        [Tooltip("頭またはカメラのTransformです。前に突き出したかどうかの判定に使います。")]
        [SerializeField] private Transform 頭;

        [Tooltip("右手のTransformです。炎の発射位置と手の突き出し判定に使います。")]
        [SerializeField] private Transform 右手;

        [Header("VRハンド判定")]
        [Tooltip("右手の握り込み値を読むInput Actionです。0に近いほど開いた手、1に近いほど握った手として扱います。")]
        [SerializeField] private InputActionProperty 右手グリップ;

        [Tooltip("この値以下なら右手を開いていると判定します。")]
        [Range(0f, 1f)]
        [SerializeField] private float 開き判定しきい値 = 0.25f;

        [Tooltip("この値以上なら右手を握ったと判定します。")]
        [Range(0f, 1f)]
        [SerializeField] private float 握り判定しきい値 = 0.75f;

        [Tooltip("頭から右手がどれだけ前に出ていればチャージできるかです。")]
        [Min(0f)]
        [SerializeField] private float 突き出し距離 = 0.35f;

        [Tooltip("右手が視線方向にどれだけ近い向きへ出ている必要があるかです。")]
        [Range(-1f, 1f)]
        [SerializeField] private float 前方判定 = 0.45f;

        [Header("フレイムボム設定")]
        [Tooltip("片手を開いて前に突き出してから発射されるまでの時間です。")]
        [Min(0f)]
        [SerializeField] private float チャージ時間 = 1f;

        [Tooltip("炎が前方へ進む速度です。")]
        [Min(0f)]
        [SerializeField] private float 移動速度 = 5f;

        [Tooltip("爆発時に攻撃判定が出る半径です。")]
        [Min(0f)]
        [SerializeField] private float 爆発半径 = 2f;

        [Tooltip("爆発で与えるダメージ量です。")]
        [Min(0f)]
        [SerializeField] private float ダメージ = 30f;

        [Tooltip("爆発時に相手を押し出す強さです。")]
        [Min(0f)]
        [SerializeField] private float ノックバック力 = 10f;

        [Tooltip("命中対象にするレイヤーです。")]
        [SerializeField] private LayerMask 命中レイヤー = ~0;

        [Tooltip("手を開いたままでもこの時間を超えると炎を消します。")]
        [Min(0.1f)]
        [SerializeField] private float 最大飛行時間 = 8f;

        [Header("デバッグ")]
        [Tooltip("飛行中の炎をその場で爆発させるデバッグ用キーです。Noneなら使いません。")]
        [SerializeField] private Key デバッグ爆発キー = Key.None;

        [Header("見た目")]
        [Tooltip("飛んでいく炎の見た目Prefabです。未設定の場合は簡易表示を生成します。")]
        [SerializeField] private GameObject 炎エフェクトPrefab;

        [Tooltip("爆発時の見た目Prefabです。未設定の場合は簡易表示を生成します。")]
        [SerializeField] private GameObject 爆発エフェクトPrefab;

        [Tooltip("爆発時にビルボード表示する動画です。設定されている場合はPrefabより優先します。")]
        [SerializeField] private VideoClip 爆発動画;

        [Tooltip("爆発動画の音量です。")]
        [Range(0f, 1f)]
        [SerializeField] private float 爆発動画音量 = 1f;

        [Tooltip("爆発動画の再生速度です。")]
        [Min(0.1f)]
        [SerializeField] private float 爆発動画再生速度 = 1f;

        [Tooltip("爆発動画ビルボードの高さです。横幅は動画の元サイズ比率に合わせます。")]
        [Min(0.1f)]
        [SerializeField] private float 爆発動画サイズ = 3f;

        [Tooltip("爆発エフェクトを消すまでの時間です。")]
        [Min(0.1f)]
        [SerializeField] private float 爆発エフェクト表示時間 = 1.2f;

        private readonly HashSet<Transform> damagedRoots = new HashSet<Transform>();

        private FlameBombState state;
        private float chargeStartedAt;
        private float flightStartedAt;
        private AbilityActivationData activeActivation;
        private GameObject activeEffect;

        private void OnEnable()
        {
            EnableAction(右手グリップ);
            ResetState();
        }

        private void OnDisable()
        {
            CancelActiveEffect();
            ResetState();
        }

        private void Update()
        {
            var context = CreateContext();
            var handIsOpen = IsRightHandOpen();
            var handIsClosed = IsRightHandClosed();
            var handIsForward = IsRightHandPushedForward(context);

            switch (state)
            {
                case FlameBombState.Waiting:
                    UpdateWaiting(context, handIsOpen, handIsForward);
                    break;
                case FlameBombState.Charging:
                    UpdateCharging(context, handIsOpen, handIsForward);
                    break;
                case FlameBombState.Flying:
                    UpdateFlying(handIsOpen, handIsClosed);
                    break;
            }
        }

        protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
        {
            CancelActiveEffect();

            activeActivation = CreateRightHandActivation(activation.AbilityId, context);
            activeEffect = CreateFlameEffect(activeActivation.Origin, activeActivation.Rotation);
            flightStartedAt = Time.time;
            state = FlameBombState.Flying;
        }

        private void UpdateWaiting(in AbilityContext context, bool handIsOpen, bool handIsForward)
        {
            if (!handIsOpen || !handIsForward)
            {
                return;
            }

            chargeStartedAt = Time.time;
            state = FlameBombState.Charging;
        }

        private void UpdateCharging(in AbilityContext context, bool handIsOpen, bool handIsForward)
        {
            if (!handIsOpen || !handIsForward)
            {
                state = FlameBombState.Waiting;
                return;
            }

            if (Time.time - chargeStartedAt < チャージ時間)
            {
                return;
            }

            TryActivate(context);
        }

        private void UpdateFlying(bool handIsOpen, bool handIsClosed)
        {
            if (activeEffect == null)
            {
                ResetState();
                return;
            }

            if (handIsClosed || IsDebugExplosionKeyPressed())
            {
                Explode(activeEffect.transform.position);
                return;
            }

            if (!handIsOpen || Time.time - flightStartedAt >= 最大飛行時間)
            {
                CancelActiveEffect();
                ResetState();
                return;
            }

            activeEffect.transform.position += activeActivation.Direction * (移動速度 * Time.deltaTime);
        }

        private AbilityContext CreateContext()
        {
            var owner = 所有者 != null ? 所有者 : gameObject;
            return new AbilityContext(owner, 頭, null, 右手);
        }

        private AbilityActivationData CreateRightHandActivation(string abilityId, in AbilityContext context)
        {
            var originSource = context.RightHand != null ? context.RightHand : context.AimSource;
            var directionSource = context.Head != null ? context.Head : originSource;

            return new AbilityActivationData(
                abilityId,
                originSource.position,
                directionSource.forward,
                Quaternion.LookRotation(directionSource.forward, Vector3.up),
                context.Owner);
        }

        private bool IsRightHandOpen()
        {
            return ReadGripValue() <= 開き判定しきい値;
        }

        private bool IsRightHandClosed()
        {
            return ReadGripValue() >= 握り判定しきい値;
        }

        private float ReadGripValue()
        {
            var action = 右手グリップ.action;
            if (action == null)
            {
                return 1f;
            }

            return Mathf.Clamp01(action.ReadValue<float>());
        }

        private bool IsDebugExplosionKeyPressed()
        {
            if (デバッグ爆発キー == Key.None)
            {
                return false;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }

            var keyControl = keyboard[デバッグ爆発キー];
            return keyControl != null && keyControl.wasPressedThisFrame;
        }

        private bool IsRightHandPushedForward(in AbilityContext context)
        {
            if (context.RightHand == null || context.AimSource == null)
            {
                return false;
            }

            var forward = Vector3.ProjectOnPlane(context.AimSource.forward, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = context.AimSource.forward;
            }
            forward.Normalize();

            var handOffset = context.RightHand.position - context.AimSource.position;
            var forwardDistance = Vector3.Dot(handOffset, forward);
            var handDirection = handOffset.sqrMagnitude > 0.0001f ? handOffset.normalized : forward;

            return forwardDistance >= 突き出し距離 && Vector3.Dot(handDirection, forward) >= 前方判定;
        }

        private GameObject CreateFlameEffect(Vector3 position, Quaternion rotation)
        {
            if (炎エフェクトPrefab != null)
            {
                return Instantiate(炎エフェクトPrefab, position, rotation);
            }

            var effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            effect.name = "Flame Bomb Effect";
            effect.transform.SetPositionAndRotation(position, rotation);
            effect.transform.localScale = Vector3.one * 0.35f;

            var collider = effect.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = effect.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(1f, 0.32f, 0.05f, 1f);
            }

            return effect;
        }

        private void Explode(Vector3 center)
        {
            CreateExplosionEffect(center);
            ApplyExplosionDamage(center);
            CancelActiveEffect();
            ResetState();
        }

        private void ApplyExplosionDamage(Vector3 center)
        {
            damagedRoots.Clear();

            var hits = Physics.OverlapSphere(center, 爆発半径, 命中レイヤー, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                if (hit == null)
                {
                    continue;
                }

                if (activeActivation.Source != null && hit.transform.IsChildOf(activeActivation.Source.transform))
                {
                    continue;
                }

                var root = hit.attachedRigidbody != null ? hit.attachedRigidbody.transform : hit.transform.root;
                if (root != null && !damagedRoots.Add(root))
                {
                    continue;
                }

                var direction = hit.transform.position - center;
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = activeActivation.Direction;
                }

                var damageData = new AbilityDamage(
                    activeActivation.AbilityId,
                    ダメージ,
                    direction.normalized,
                    ノックバック力,
                    hit.ClosestPoint(center),
                    activeActivation.Source);

                AbilityDamageUtility.TryApplyDamage(hit, damageData);
            }
        }

        private void CreateExplosionEffect(Vector3 center)
        {
            GameObject effect;
            if (爆発動画 != null)
            {
                FlameBombExplosionVideoBillboard.Create(
                    爆発動画,
                    center,
                    頭,
                    爆発動画サイズ,
                    爆発動画音量,
                    爆発動画再生速度);
                return;
            }
            else if (爆発エフェクトPrefab != null)
            {
                effect = Instantiate(爆発エフェクトPrefab, center, Quaternion.identity);
            }
            else
            {
                effect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                effect.name = "Flame Bomb Explosion";
                effect.transform.position = center;
                effect.transform.localScale = Vector3.one * (爆発半径 * 2f);

                var collider = effect.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                var renderer = effect.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(1f, 0.12f, 0.02f, 0.6f);
                }
            }

            Destroy(effect, 爆発エフェクト表示時間);
        }

        private void CancelActiveEffect()
        {
            if (activeEffect != null)
            {
                Destroy(activeEffect);
                activeEffect = null;
            }
        }

        private void ResetState()
        {
            state = FlameBombState.Waiting;
            chargeStartedAt = 0f;
            flightStartedAt = 0f;
            damagedRoots.Clear();
        }

        private static void EnableAction(InputActionProperty actionProperty)
        {
            var action = actionProperty.action;
            if (action != null && !action.enabled)
            {
                action.Enable();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (activeEffect == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.2f, 0f, 0.35f);
            Gizmos.DrawWireSphere(activeEffect.transform.position, 爆発半径);
        }
    }

    internal sealed class FlameBombExplosionVideoBillboard : MonoBehaviour
    {
        private Transform target;
        private RenderTexture renderTexture;
        private Material videoMaterial;

        public static GameObject Create(
            VideoClip clip,
            Vector3 position,
            Transform lookTarget,
            float size,
            float volume,
            float playbackSpeed)
        {
            var billboard = GameObject.CreatePrimitive(PrimitiveType.Quad);
            billboard.name = "Flame Bomb Explosion Video";
            billboard.transform.position = position;
            billboard.transform.localScale = CalculateBillboardScale(clip, size);

            var collider = billboard.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var controller = billboard.AddComponent<FlameBombExplosionVideoBillboard>();
            controller.Initialize(clip, lookTarget, volume, playbackSpeed);

            var duration = clip != null
                ? Mathf.Max(0.1f, (float)(clip.length / Mathf.Max(0.1f, playbackSpeed)))
                : 0.1f;
            Destroy(billboard, duration + 0.25f);

            return billboard;
        }

        private static Vector3 CalculateBillboardScale(VideoClip clip, float height)
        {
            var safeHeight = Mathf.Max(0.1f, height);
            if (clip == null || clip.height == 0)
            {
                return Vector3.one * safeHeight;
            }

            var aspect = Mathf.Max(0.01f, clip.width / (float)clip.height);
            return new Vector3(safeHeight * aspect, safeHeight, 1f);
        }

        private void Initialize(VideoClip clip, Transform lookTarget, float volume, float playbackSpeed)
        {
            target = lookTarget;

            var textureSize = CalculateRenderTextureSize(clip);
            renderTexture = new RenderTexture(textureSize.x, textureSize.y, 0, RenderTextureFormat.ARGB32);
            renderTexture.Create();

            var shader = Shader.Find("Unlit/Texture");
            videoMaterial = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
            videoMaterial.mainTexture = renderTexture;
            videoMaterial.SetInt("_Cull", (int)CullMode.Off);

            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                meshRenderer.material = videoMaterial;
            }

            var audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.volume = Mathf.Clamp01(volume);

            var videoPlayer = gameObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.clip = clip;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.targetTexture = renderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
            videoPlayer.playbackSpeed = Mathf.Max(0.1f, playbackSpeed);
            videoPlayer.controlledAudioTrackCount = 1;
            videoPlayer.EnableAudioTrack(0, true);
            videoPlayer.SetTargetAudioSource(0, audioSource);
            videoPlayer.Play();
        }

        private static Vector2Int CalculateRenderTextureSize(VideoClip clip)
        {
            const int maxSide = 1024;
            if (clip == null || clip.width == 0 || clip.height == 0)
            {
                return new Vector2Int(maxSide, maxSide);
            }

            var width = (int)clip.width;
            var height = (int)clip.height;
            if (width >= height)
            {
                var scaledHeight = Mathf.Max(1, Mathf.RoundToInt(maxSide * (height / (float)width)));
                return new Vector2Int(maxSide, scaledHeight);
            }

            var scaledWidth = Mathf.Max(1, Mathf.RoundToInt(maxSide * (width / (float)height)));
            return new Vector2Int(scaledWidth, maxSide);
        }

        private void LateUpdate()
        {
            var lookSource = target != null
                ? target
                : Camera.main != null
                    ? Camera.main.transform
                    : null;

            if (lookSource == null)
            {
                return;
            }

            var direction = lookSource.position - transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void OnDestroy()
        {
            if (videoMaterial != null)
            {
                Destroy(videoMaterial);
            }

            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }
    }
}
