using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerMeleeCombat : NetworkBehaviour
{
    public enum AttackType { Straight, Overhead, Uppercut }
    public enum PvPMode { Disabled, FreeForAll, TeamBased }

    [System.Serializable]
    public struct AttackData
    {
        [Header("Animation")]
        public string animationTrigger;

        [Header("Combat")]
        public float damage;
        public float knockbackForce;
        public Vector3 halfExtents;
        public Vector3 localOffset;
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

    private void Update()
    {
        if (!IsSpawned || !IsOwner) return;

        if (Input.GetMouseButtonDown(0))
            TryAttack(AttackType.Straight);
    }

    public void TryAttack(AttackType type)
    {
        if (!IsOwner) return;

        AttackData data = GetAttackData(type);
        if (uiAnimator != null && !string.IsNullOrWhiteSpace(data.animationTrigger))
            uiAnimator.SetTrigger(data.animationTrigger);

        PlayAttackAnimClientRpc((int)type);
    }

    [ClientRpc]
    private void PlayAttackAnimClientRpc(int attackTypeInt)
    {
        if (IsOwner) return; // dono já animou localmente

        AttackType type = (AttackType)attackTypeInt;
        AttackData data = GetAttackData(type);

        if (uiAnimator != null && !string.IsNullOrWhiteSpace(data.animationTrigger))
            uiAnimator.SetTrigger(data.animationTrigger);
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

        AttackData data = GetAttackData(type);
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
            if (col.transform.root == transform.root) continue;
            if (singleHitPerSwing && _alreadyHit.Contains(col)) continue;
            _alreadyHit.Add(col);

            var stamina = col.GetComponentInParent<PlayerStamina>();
            if (stamina != null)
            {
                var targetNetworkObject = stamina.GetComponent<NetworkObject>();
                if (!CanHitPlayer(targetNetworkObject)) continue;

                if (singleHitPerSwing)
                {
                    ulong playerId = targetNetworkObject.NetworkObjectId;
                    if (_alreadyHitPlayers.Contains(playerId)) continue;
                    _alreadyHitPlayers.Add(playerId);
                }

                stamina.ReceivePushDamage(data.damage);

                var movement = stamina.GetComponent<MovementPlayer>();
                if (movement != null)
                {
                    Vector3 forceDir = GetKnockbackDirection(type);
                    movement.AddPush(forceDir, data.knockbackForce);
                }
            }
            else
            {
                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null) damageable.TakeDamage(data.damage);
                else col.transform.root.SendMessage("TakeDamage", data.damage, SendMessageOptions.DontRequireReceiver);
            }

            Rigidbody rb = col.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                Vector3 forceDir = GetKnockbackDirection(type);
                rb.AddForce(forceDir * data.knockbackForce, ForceMode.Impulse);
            }

            _hitBuffer[i] = null;
        }
    }

    private bool CanHitPlayer(NetworkObject targetNetworkObject)
    {
        if (targetNetworkObject == null) return false;
        if (targetNetworkObject.NetworkObjectId == NetworkObjectId) return false;

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
        teamId = -1;
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

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmo || playerCamera == null) return;

        AttackData data = GetAttackData(debugAttackType);
        Transform cam = playerCamera.transform;
        Vector3 center = cam.TransformPoint(data.localOffset);
        Quaternion rot = cam.rotation;

        Matrix4x4 old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, rot, Vector3.one);

        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(Vector3.zero, data.halfExtents * 2f);
        Gizmos.DrawWireCube(Vector3.zero, data.halfExtents * 2f);

        Gizmos.matrix = old;
    }
}

public interface IDamageable
{
    void TakeDamage(float amount);
}

public interface ITeamProvider
{
    int TeamId { get; }
}
