using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.Serialization;

public class PlayerMeleeCombat : NetworkBehaviour
{
    public enum AttackType { Straight, Overhead, Uppercut }
    public enum PvPMode { Disabled, FreeForAll, TeamBased }

    [System.Serializable]
    public struct AttackData
    {
        [Header("Attack Preset")]
        [Tooltip("Preset novo do ataque. Se preenchido, define animacao, dano, hitbox, timing e pode definir PushPreset.")]
        public AttackPreset attackPresetAsset;

        [Header("Animation")]
        public string animationTrigger;

        [Header("Combat")]
        public float damage;
        [Tooltip("Preset novo de empurrao. Se preenchido, define forca, duracao, desaceleracao, controle e parede.")]
        public PushPreset pushPresetAsset;
        [Header("Legacy Push")]
        [FormerlySerializedAs("pushPreset")]
        public LegacyPushPreset legacyPushPreset;
        [Tooltip("Forca antiga de knockback. Usada quando PushPreset novo nao esta atribuido.")]
        public float knockbackForce;
        [Tooltip("Duracao antiga do impacto. Usada quando PushPreset novo nao esta atribuido.")]
        public float impactDuration;
        [Range(0f, 1f)] public float targetControlMultiplier;
        [Tooltip("Velocidade externa antiga. Usada quando PushPreset novo nao esta atribuido.")]
        public float maxExternalSpeed;
        public Vector3 halfExtents;
        public Vector3 localOffset;

        [Header("Legacy Timing")]
        [Tooltip("Cooldown antigo usado quando AttackPreset nao esta atribuido.")]
        public float cooldown;
        [Tooltip("Tempo de recuperacao antigo. Guardado para compatibilidade e Fase 4.")]
        public float recoveryTime;
        [Tooltip("Janela futura de combo. Ainda nao executa combo nesta fase.")]
        public float comboWindow;
        [Tooltip("Dado futuro: se ligado, combo so deve continuar se acertar.")]
        public bool requireHitToContinueCombo;
    }

    private struct ResolvedAttackData
    {
        public AttackPresetType attackPresetType;
        public string animationTrigger;
        public float damage;
        public PushPreset pushPresetAsset;
        public LegacyPushPreset legacyPushPreset;
        public float knockbackForce;
        public float impactDuration;
        public float targetControlMultiplier;
        public float maxExternalSpeed;
        public Vector3 halfExtents;
        public Vector3 localOffset;
        public float cooldown;
        public float recoveryTime;
        public float comboWindow;
        public bool requireHitToContinueCombo;
        public bool singleHitPerSwing;
        public Color debugColor;
    }

    [Header("References")]
    public Animator uiAnimator;
    public Camera playerCamera;

    [Header("Detection")]
    public LayerMask hitMask;
    public bool singleHitPerSwing = true;

    [Header("Attack Config")]
    public AttackData straight;
    public AttackData overhead;
    public AttackData uppercut;

    [Header("PvP Rules")]
    [SerializeField] private PvPMode pvpMode = PvPMode.FreeForAll;
    [SerializeField] private bool allowFriendlyFire = false;

    [Header("Debug")]
    public bool drawDebugGizmo = true;
    public AttackType debugAttackType = AttackType.Straight;
    public Color gizmoColor = new Color(1f, 0f, 0f, 0.35f);

    private readonly Collider[] _hitBuffer = new Collider[32];
    private readonly HashSet<Collider> _alreadyHit = new();
    private readonly HashSet<ulong> _alreadyHitPlayers = new();
    private readonly float[] _nextAttackTimes = new float[System.Enum.GetValues(typeof(AttackType)).Length];
    private const int InvalidTeamId = -1;
    private const float DefaultLegacyCooldown = 0.35f;

    private void Update()
    {
        if (!IsSpawned || !IsOwner) return;

        if (Input.GetMouseButtonDown(0))
            TryAttack(AttackType.Straight);
    }

    public void TryAttack(AttackType type)
    {
        if (!IsOwner) return;

        ResolvedAttackData data = ResolveAttackData(type);
        int attackIndex = (int)type;
        if (Time.time < _nextAttackTimes[attackIndex]) return;

        _nextAttackTimes[attackIndex] = Time.time + Mathf.Max(0f, data.cooldown);

        TrySetAnimationTrigger(data.animationTrigger);

        PlayAttackAnimClientRpc((int)type);
    }

    [ClientRpc]
    private void PlayAttackAnimClientRpc(int attackTypeInt)
    {
        if (IsOwner) return; // dono já animou localmente

        AttackType type = (AttackType)attackTypeInt;
        ResolvedAttackData data = ResolveAttackData(type);

        TrySetAnimationTrigger(data.animationTrigger);
    }

    // Animation Events
    public void AE_Hit_Straight() => RequestHit(AttackType.Straight);
    public void AE_Hit_Overhead() => RequestHit(AttackType.Overhead);
    public void AE_Hit_Uppercut() => RequestHit(AttackType.Uppercut);

    private void RequestHit(AttackType type)
    {
        if (!IsOwner) return;
        PerformHitServerRpc((int)type);
    }

    [ServerRpc]
    private void PerformHitServerRpc(int attackTypeInt)
    {
        AttackType type = (AttackType)attackTypeInt;
        PerformHitServer(type);
    }

    private void PerformHitServer(AttackType type)
    {
        if (playerCamera == null) return;

        ResolvedAttackData data = ResolveAttackData(type);
        Vector3 center = playerCamera.transform.TransformPoint(data.localOffset);
        Quaternion rotation = playerCamera.transform.rotation;

        _alreadyHit.Clear();
        _alreadyHitPlayers.Clear();

        int hitCount = Physics.OverlapBoxNonAlloc(
            center, data.halfExtents, _hitBuffer, rotation, hitMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _hitBuffer[i];
            if (col == null) continue;

            var targetNetworkObject = col.GetComponentInParent<NetworkObject>();
            if (targetNetworkObject != null && targetNetworkObject.NetworkObjectId == NetworkObjectId) continue;

            if (data.singleHitPerSwing && _alreadyHit.Contains(col)) continue;
            _alreadyHit.Add(col);

            bool appliedImpact = false;
            Vector3 forceDir = GetKnockbackDirection(type);
            ImpactData impact = CreateImpactData(data, forceDir);
            float damage = impact.damage;
            float force = impact.force;

            var damageable = col.GetComponentInParent<IDamageable>();
            var impactReceiver = col.GetComponentInParent<IImpactReceiver>();

            if (targetNetworkObject != null)
            {
                if (!CanHitPlayer(targetNetworkObject)) continue;

                ulong playerId = targetNetworkObject.NetworkObjectId;
                if (_alreadyHitPlayers.Contains(playerId)) continue;
                _alreadyHitPlayers.Add(playerId);
            }

            if (damageable != null)
                damageable.TakeDamage(damage);
            else
                // Legacy fallback for older scene objects that still expose TakeDamage without IDamageable.
                // Remove after all combat targets are migrated to IDamageable.
                col.transform.root.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);

            if (impactReceiver != null)
            {
                impactReceiver.ReceiveImpact(impact);
                appliedImpact = true;
            }

            Rigidbody rb = col.attachedRigidbody;
            if (!appliedImpact && rb != null && !rb.isKinematic)
                rb.AddForce(forceDir * force, ForceMode.Impulse);

            _hitBuffer[i] = null;
        }
    }

    private bool CanHitPlayer(NetworkObject targetNetworkObject)
    {
        if (targetNetworkObject == null) return false;

        switch (pvpMode)
        {
            case PvPMode.Disabled:
                return false;

            case PvPMode.TeamBased:
                if (allowFriendlyFire) return true;

                if (TryGetTeamId(NetworkObject, out int myTeam) &&
                    TryGetTeamId(targetNetworkObject, out int targetTeam) &&
                    myTeam == targetTeam)
                {
                    return false;
                }
                return true;

            default:
                return true;
        }
    }

    private static bool TryGetTeamId(NetworkObject networkObject, out int teamId)
    {
        teamId = InvalidTeamId;
        if (networkObject == null) return false;

        var teamProvider = networkObject.GetComponent<ITeamProvider>();
        if (teamProvider == null) return false;

        teamId = teamProvider.TeamId;
        return true;
    }

    private Vector3 GetKnockbackDirection(AttackType type)
    {
        Vector3 forward = playerCamera.transform.forward;
        switch (type)
        {
            case AttackType.Uppercut: return (forward + Vector3.up * 0.9f).normalized;
            case AttackType.Overhead: return (forward + Vector3.down * 0.15f).normalized;
            default: return forward.normalized;
        }
    }

    private AttackData GetAttackData(AttackType type)
    {
        return type switch
        {
            AttackType.Straight => straight,
            AttackType.Overhead => overhead,
            AttackType.Uppercut => uppercut,
            _ => straight
        };
    }

    private void TrySetAnimationTrigger(string triggerName)
    {
        if (uiAnimator == null || string.IsNullOrWhiteSpace(triggerName)) return;

        foreach (AnimatorControllerParameter parameter in uiAnimator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger &&
                parameter.name == triggerName)
            {
                uiAnimator.SetTrigger(triggerName);
                return;
            }
        }

        Debug.LogWarning($"[PlayerMeleeCombat] Animator trigger '{triggerName}' nao existe no Animator atual.", this);
    }

    private ResolvedAttackData ResolveAttackData(AttackType type)
    {
        AttackData data = GetAttackData(type);
        AttackPreset preset = data.attackPresetAsset;

        if (preset != null)
        {
            return new ResolvedAttackData
            {
                attackPresetType = preset.AttackType,
                animationTrigger = preset.AnimationTrigger,
                damage = preset.Damage,
                pushPresetAsset = preset.PushPreset != null ? preset.PushPreset : data.pushPresetAsset,
                legacyPushPreset = data.legacyPushPreset,
                knockbackForce = data.knockbackForce,
                impactDuration = data.impactDuration,
                targetControlMultiplier = data.targetControlMultiplier,
                maxExternalSpeed = data.maxExternalSpeed,
                halfExtents = preset.HalfExtents,
                localOffset = preset.LocalOffset,
                cooldown = preset.Cooldown,
                recoveryTime = preset.RecoveryTime,
                comboWindow = preset.ComboWindow,
                requireHitToContinueCombo = preset.RequireHitToContinueCombo,
                singleHitPerSwing = preset.SingleHitPerSwing,
                debugColor = preset.DebugColor
            };
        }

        return new ResolvedAttackData
        {
            attackPresetType = AttackPresetType.SimplePunch,
            animationTrigger = data.animationTrigger,
            damage = data.damage,
            pushPresetAsset = data.pushPresetAsset,
            legacyPushPreset = data.legacyPushPreset,
            knockbackForce = data.knockbackForce,
            impactDuration = data.impactDuration,
            targetControlMultiplier = data.targetControlMultiplier,
            maxExternalSpeed = data.maxExternalSpeed,
            halfExtents = data.halfExtents,
            localOffset = data.localOffset,
            cooldown = data.cooldown > 0f ? data.cooldown : DefaultLegacyCooldown,
            recoveryTime = data.recoveryTime,
            comboWindow = data.comboWindow,
            requireHitToContinueCombo = data.requireHitToContinueCombo,
            singleHitPerSwing = singleHitPerSwing,
            debugColor = gizmoColor
        };
    }

    private ImpactData CreateImpactData(ResolvedAttackData data, Vector3 forceDir)
    {
        if (data.pushPresetAsset != null)
            return data.pushPresetAsset.CreateImpactData(data.damage, forceDir, playerCamera.transform.position);

        return new ImpactData(
            data.damage,
            forceDir,
            GetLegacyKnockbackForce(data),
            data.impactDuration,
            data.maxExternalSpeed,
            data.targetControlMultiplier,
            playerCamera.transform.position);
    }

    private float GetLegacyKnockbackForce(ResolvedAttackData data)
    {
        if (data.legacyPushPreset != LegacyPushPreset.Custom)
        {
            float presetForce = GetLegacyPushForce(data.legacyPushPreset);
            if (presetForce > 0f) return presetForce;
        }

        return data.knockbackForce;
    }

    private float GetLegacyPushForce(LegacyPushPreset preset)
    {
        var movement = GetComponent<MovementPlayer>();
        if (movement == null) return 0f;

        return movement.GetPushForce(preset);
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmo || playerCamera == null) return;

        ResolvedAttackData data = ResolveAttackData(debugAttackType);
        Transform cam = playerCamera.transform;
        Vector3 center = cam.TransformPoint(data.localOffset);
        Quaternion rot = cam.rotation;

        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);

        Gizmos.color = data.debugColor;
        Gizmos.DrawCube(Vector3.zero, data.halfExtents * 2f);
        Gizmos.DrawWireCube(Vector3.zero, data.halfExtents * 2f);

        Gizmos.matrix = old;
    }
}

public interface ITeamProvider
{
    int TeamId { get; }
}
