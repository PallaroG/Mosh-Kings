using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMeleeCombat : MonoBehaviour
{
    public enum AttackType
    {
        Straight,
        Overhead,
        Uppercut
    }

    [System.Serializable]
    public struct AttackData
    {
        [Header("Animation")]
        public string animationTrigger;

        [Header("Combat")]
        public float damage;
        public float knockbackForce;

        [Tooltip("Metade do tamanho da caixa do golpe (como no OverlapBox).")]
        public Vector3 halfExtents;

        [Tooltip("Offset local em relação à câmera (x=lado, y=altura, z=frente).")]
        public Vector3 localOffset;
    }

    [Header("References (arrastar no Inspector)")]
    [Tooltip("Animator da UI/arma/mãos em primeira pessoa.")]
    public Animator uiAnimator;

    [Tooltip("Câmera principal do jogador (FPS Camera).")]
    public Camera playerCamera;

    [Header("Detection")]
    public LayerMask hitMask;

    [Tooltip("Evita múltiplos hits no mesmo alvo dentro do mesmo evento de ataque.")]
    public bool singleHitPerSwing = true;

    [Header("Attack Config")]
    public AttackData straight;
    public AttackData overhead;
    public AttackData uppercut;

    [Header("Debug")]
    public bool drawDebugGizmo = true;
    public AttackType debugAttackType = AttackType.Straight;
    public Color gizmoColor = new Color(1f, 0f, 0f, 0.35f);

    private readonly Collider[] _hitBuffer = new Collider[32];
    private readonly HashSet<Collider> _alreadyHit = new HashSet<Collider>();

    #region Public API (input -> animação)

    public void TryAttack(AttackType type)
    {
        if (uiAnimator == null) return;

        AttackData data = GetAttackData(type);

        if (!string.IsNullOrWhiteSpace(data.animationTrigger))
        {
            uiAnimator.SetTrigger(data.animationTrigger);
        }
    }

    private void Update()
    {
        // Detecta o clique usando o Input Manager clássico
        if (Input.GetMouseButtonDown(0))
        {
            TryAttack(AttackType.Straight);
        }
    }

    #endregion

    #region Animation Events

    public void AE_Hit_Straight()
    {
        PerformHit(AttackType.Straight);
    }

    public void AE_Hit_Overhead()
    {
        PerformHit(AttackType.Overhead);
    }

    public void AE_Hit_Uppercut()
    {
        PerformHit(AttackType.Uppercut);
    }

    #endregion

    private void PerformHit(AttackType type)
    {
        if (playerCamera == null) return;

        AttackData data = GetAttackData(type);

        Vector3 center = GetWorldCenter(data.localOffset);
        Quaternion rotation = playerCamera.transform.rotation;

        _alreadyHit.Clear();

        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            data.halfExtents,
            _hitBuffer,
            rotation,
            hitMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _hitBuffer[i];
            if (col == null) continue;

            if (singleHitPerSwing && _alreadyHit.Contains(col))
                continue;

            _alreadyHit.Add(col);

            var damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(data.damage);
            }
            else
            {
                col.transform.root.SendMessage("TakeDamage", data.damage, SendMessageOptions.DontRequireReceiver);
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

    private Vector3 GetWorldCenter(Vector3 localOffset)
    {
        return playerCamera.transform.TransformPoint(localOffset);
    }

    private Vector3 GetKnockbackDirection(AttackType type)
    {
        Vector3 forward = playerCamera.transform.forward;
        Vector3 up = Vector3.up;

        switch (type)
        {
            case AttackType.Uppercut:
                return (forward + up * 0.9f).normalized;
            case AttackType.Overhead:
                return (forward + Vector3.down * 0.15f).normalized;
            default:
                return forward.normalized;
        }
    }

    private AttackData GetAttackData(AttackType type)
    {
        switch (type)
        {
            case AttackType.Straight: return straight;
            case AttackType.Overhead: return overhead;
            case AttackType.Uppercut: return uppercut;
            default: return straight;
        }
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