using Fusion;
using UnityEngine;
using Wapawapa.Gameplay;

namespace Wapawapa.Abilities
{
    public sealed class TeleportAbility : AbilityBase
    {
        [Header("Teleport")]
        [Tooltip("目線方向の何m先へテレポートするかです。")]
        [Min(0f)]
        [InspectorName("テレポート距離")]
        [SerializeField] private float teleportDistance = 5f;

        [Tooltip("テレポート後、移動できない秒数です。視点操作は止めません。")]
        [Min(0f)]
        [InspectorName("移動不能時間")]
        [SerializeField] private float movementLockSeconds = 1.5f;

        [Tooltip("地面判定に使うレイヤーです。")]
        [InspectorName("地面レイヤー")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("目標地点から上にどれだけ離れた場所から地面を探すかです。")]
        [Min(0f)]
        [InspectorName("地面探索の上方向距離")]
        [SerializeField] private float groundProbeUp = 8f;

        [Tooltip("目標地点から下にどれだけ地面を探すかです。")]
        [Min(0f)]
        [InspectorName("地面探索の下方向距離")]
        [SerializeField] private float groundProbeDown = 30f;

        [Tooltip("地面から少し浮かせる量です。埋まり防止に使います。")]
        [Min(0f)]
        [InspectorName("地面からの余白")]
        [SerializeField] private float groundClearance = 0.03f;

        [Header("Sound")]
        [Tooltip("テレポート発動時に鳴らすSEです。")]
        [InspectorName("発動SE")]
        [SerializeField] private AudioClip activationSound;

        [Min(0f)]
        [InspectorName("発動SE音量")]
        [SerializeField] private float activationVolume = 1f;

        [SerializeField, Range(0f, 1f)]
        [InspectorName("発動SEの3D感")]
        private float activationSpatialBlend = 0.2f;

        [Min(0f)]
        [InspectorName("発動SEの最小距離")]
        [SerializeField] private float activationMinDistance = 4f;

        [Min(0.01f)]
        [InspectorName("発動SEの最大距離")]
        [SerializeField] private float activationMaxDistance = 25f;

        protected override void Activate(in AbilityContext context, in AbilityActivationData activation)
        {
            if (context.Owner == null || !CanMoveOwner(context.Owner))
            {
                return;
            }

            var destination = ResolveDestination(context.Owner, activation);
            TeleportOwner(context.Owner, destination);
            PlayActivationSound(destination);

            var movementLock = context.Owner.GetComponent<IPlayerMovementLock>();
            movementLock?.LockMovement(movementLockSeconds);
        }

        private Vector3 ResolveDestination(GameObject owner, in AbilityActivationData activation)
        {
            var forward = Vector3.ProjectOnPlane(activation.Direction, Vector3.up);
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(owner.transform.forward, Vector3.up);
            }

            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            var target = owner.transform.position + forward * teleportDistance;
            var rayOrigin = target + Vector3.up * groundProbeUp;
            var rayDistance = groundProbeUp + groundProbeDown;
            if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                target = hit.point + Vector3.up * groundClearance;
            }

            return target;
        }

        private static void TeleportOwner(GameObject owner, Vector3 destination)
        {
            var characterController = owner.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
                owner.transform.position = destination;
                characterController.enabled = true;
                return;
            }

            owner.transform.position = destination;
        }

        private void PlayActivationSound(Vector3 position)
        {
            if (activationSound == null || activationVolume <= 0f)
            {
                return;
            }

            var audioObject = new GameObject("Teleport Activation Sound");
            audioObject.transform.position = position;

            var source = audioObject.AddComponent<AudioSource>();
            source.clip = activationSound;
            source.volume = activationVolume;
            source.spatialBlend = activationSpatialBlend;
            source.minDistance = activationMinDistance;
            source.maxDistance = activationMaxDistance;
            source.Play();

            Destroy(audioObject, activationSound.length + 0.1f);
        }

        private static bool CanMoveOwner(GameObject owner)
        {
            var networkObject = owner.GetComponentInParent<NetworkObject>();
            return networkObject == null || !networkObject.IsValid || networkObject.HasStateAuthority;
        }
    }
}
