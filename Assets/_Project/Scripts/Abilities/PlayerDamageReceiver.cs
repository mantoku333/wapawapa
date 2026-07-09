using UnityEngine;
using UnityEngine.Events;
using Fusion;
using Wapawapa.Gameplay;
using Wapawapa.UI;

namespace Wapawapa.Abilities
{
    public sealed class PlayerDamageReceiver : NetworkBehaviour, IAbilityDamageReceiver
    {
        private const string BasicPunchAbilityId = "basic.punch";

        [Header("プレイヤー体力")]
        [Tooltip("プレイヤーの最大体力です。パンチやアビリティのダメージで減ります。")]
        [SerializeField] private float maxHealth = 100f;

        [Tooltip("体力が0になった時に呼ばれるイベントです。演出やリスポーン処理を後から接続できます。")]
        [SerializeField] private UnityEvent onKnockedOut;

        private float localHealth;
        private bool knockedOutNotified;
        private NetworkObject networkObject;

        [Networked] public float NetworkedHealth { get; set; }
        [Networked] public NetworkBool NetworkedHealthReady { get; set; }
        [Networked] public NetworkString<_32> NetworkedPlayerName { get; set; }
        [Networked] public NetworkBool NetworkedPlayerNameReady { get; set; }

        public static event System.Action<PlayerDamageReceiver> KnockedOut;

        public event System.Action<PlayerDamageReceiver, AbilityDamage, PlayerRef> Damaged;
        public event System.Action<PlayerDamageReceiver> HealthChanged;

        public float Health => IsNetworked ? (NetworkedHealthReady ? NetworkedHealth : maxHealth) : localHealth;
        public float MaxHealth => maxHealth;
        public bool IsKnockedOut => IsHealthReady && Health <= 0f;
        public bool IsLocalPlayer => !IsNetworked || networkObject.HasStateAuthority;
        public string DisplayName => IsNetworked && NetworkedPlayerNameReady
            ? NetworkedPlayerName.ToString()
            : name;

        private bool IsNetworked => networkObject != null && networkObject.IsValid;
        private bool IsHealthReady => !IsNetworked || NetworkedHealthReady;

        private void Awake()
        {
            networkObject = GetComponentInParent<NetworkObject>();
            localHealth = maxHealth;
        }

        public override void Spawned()
        {
            networkObject = Object;
            if (HasStateAuthority)
            {
                NetworkedHealth = maxHealth;
                NetworkedHealthReady = true;
            }

            ScreenHealthHud.Ensure();
        }

        private void Update()
        {
            if (!IsHealthReady)
            {
                return;
            }

            if (!IsKnockedOut)
            {
                knockedOutNotified = false;
                return;
            }

            if (IsKnockedOut && !knockedOutNotified)
            {
                NotifyKnockedOut();
            }
        }

        public void ApplyDamage(in AbilityDamage damage)
        {
            if (IsNetworked && !IsDamageFromLocalAuthority(damage.Source))
            {
                return;
            }

            var attacker = TryGetDamageSourcePlayer(damage.Source);
            if (IsNetworked && !HasStateAuthority)
            {
                RPC_RequestDamage(
                    damage.Amount,
                    damage.Direction,
                    damage.PushForce,
                    damage.Point,
                    attacker,
                    damage.AbilityId == BloodFocusStrikeAbility.BlackFlashAbilityId,
                    damage.AbilityId == BasicPunchAbilityId);
                return;
            }

            ApplyDamageAuthoritative(damage, attacker);
        }

        public void RequestResetHealth()
        {
            if (IsNetworked && !HasStateAuthority)
            {
                RPC_RequestResetHealth();
                return;
            }

            ResetHealthAuthoritative();
        }

        public void ResetHealth()
        {
            RequestResetHealth();
        }

        public void SetPlayerName(string playerName)
        {
            var sanitizedName = string.IsNullOrWhiteSpace(playerName) ? name : playerName.Trim();
            if (sanitizedName.Length > 20)
            {
                sanitizedName = sanitizedName.Substring(0, 20);
            }

            if (IsNetworked && !HasStateAuthority)
            {
                return;
            }

            NetworkedPlayerName = sanitizedName;
            NetworkedPlayerNameReady = true;
        }

        public void RequestRespawnAtSpawnPoint()
        {
            if (IsNetworked && !HasStateAuthority)
            {
                RPC_RequestRespawnAtSpawnPoint();
                return;
            }

            RespawnAtSpawnPoint();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestDamage(float amount, Vector3 direction, float pushForce, Vector3 point, PlayerRef attacker, bool isBlackFlash, bool isBasicPunch)
        {
            var abilityId = isBlackFlash ? BloodFocusStrikeAbility.BlackFlashAbilityId : isBasicPunch ? BasicPunchAbilityId : "network.damage";
            ApplyDamageAuthoritative(new AbilityDamage(abilityId, amount, direction, pushForce, point, null), attacker);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestResetHealth()
        {
            ResetHealthAuthoritative();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestRespawnAtSpawnPoint()
        {
            RespawnAtSpawnPoint();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayDamageFeedback(PlayerRef attacker, Vector3 point, Vector3 direction, bool isBlackFlash, bool isBasicPunch)
        {
            if (isBlackFlash)
            {
                BloodFocusStrikeAbility.PlayNetworkFeedback(point, direction);
                return;
            }

            if (!isBasicPunch)
            {
                return;
            }

            if (IsLocalPlayer)
            {
                PlayerCombatAudio.PlayLocalHitTaken(transform.position);
            }

            if (Runner != null && attacker == Runner.LocalPlayer)
            {
                PlayerCombatAudio.PlayLocalHitLanded(point);
            }
        }

        private void ApplyDamageAuthoritative(in AbilityDamage damage, PlayerRef attacker)
        {
            if (damage.Amount <= 0f || IsKnockedOut)
            {
                return;
            }

            var nextHealth = Mathf.Max(0f, Health - damage.Amount);
            SetHealth(nextHealth);

            Debug.Log($"{name} took {damage.Amount:0} ability damage from {damage.AbilityId}. HP {Health:0}/{maxHealth:0}");
            Damaged?.Invoke(this, damage, attacker);

            if (IsNetworked)
            {
                RPC_PlayDamageFeedback(
                    attacker,
                    damage.Point,
                    damage.Direction,
                    damage.AbilityId == BloodFocusStrikeAbility.BlackFlashAbilityId,
                    damage.AbilityId == BasicPunchAbilityId);
            }
            else
            {
                if (damage.AbilityId == BloodFocusStrikeAbility.BlackFlashAbilityId)
                {
                    BloodFocusStrikeAbility.PlayNetworkFeedback(damage.Point, damage.Direction);
                    return;
                }

                if (damage.AbilityId != BasicPunchAbilityId)
                {
                    return;
                }

                PlayerCombatAudio.PlayLocalHitTaken(transform.position);
                PlayerCombatAudio.PlayLocalHitLanded(damage.Point);
            }

            if (IsKnockedOut)
            {
                NotifyKnockedOut();
            }
        }

        private void ResetHealthAuthoritative()
        {
            SetHealth(maxHealth);
            knockedOutNotified = false;
        }

        private void RespawnAtSpawnPoint()
        {
            if (IsNetworked && !HasStateAuthority)
            {
                return;
            }

            var stateAuthority = IsNetworked ? Object.StateAuthority : PlayerRef.None;
            var spawnPose = PlayerSpawnPoints.GetSpawnPose(stateAuthority);
            var characterController = GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            transform.SetPositionAndRotation(spawnPose.position, spawnPose.rotation);

            if (characterController != null)
            {
                characterController.enabled = true;
            }
        }

        private void SetHealth(float value)
        {
            var clamped = Mathf.Clamp(value, 0f, maxHealth);
            if (IsNetworked)
            {
                NetworkedHealthReady = true;
                NetworkedHealth = clamped;
            }
            else
            {
                localHealth = clamped;
            }

            HealthChanged?.Invoke(this);
        }

        private void NotifyKnockedOut()
        {
            knockedOutNotified = true;
            onKnockedOut?.Invoke();
            KnockedOut?.Invoke(this);
            MatchResultPanel.ShowFor(this);
        }

        private static PlayerRef TryGetDamageSourcePlayer(GameObject source)
        {
            if (source == null)
            {
                return PlayerRef.None;
            }

            var sourceObject = source.GetComponentInParent<NetworkObject>();
            return sourceObject != null ? sourceObject.StateAuthority : PlayerRef.None;
        }

        private static bool IsDamageFromLocalAuthority(GameObject source)
        {
            if (source == null)
            {
                return true;
            }

            var sourceObject = source.GetComponentInParent<NetworkObject>();
            return sourceObject == null || !sourceObject.IsValid || sourceObject.HasStateAuthority;
        }
    }
}
