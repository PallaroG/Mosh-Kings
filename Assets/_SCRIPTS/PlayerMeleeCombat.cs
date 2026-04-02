using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    // Cache para evitar alocação frequente no ataque (opcional, ajuda performance)
    private readonly Collider[] _hitBuffer = new Collider[32];
    private readonly HashSet<Collider> _alreadyHit = new HashSet<Collider>();

    #region Public API (input -> animação)

    public void TryAttack(AttackType type)
    {
        if (uiAnimator == null)
        {
            Debug.LogWarning("[PlayerMeleeCombat] uiAnimator não atribuído.");
            return;
        }

        AttackData data = GetAttackData(type);

        if (!string.IsNullOrWhiteSpace(data.animationTrigger))
        {
            uiAnimator.SetTrigger(data.animationTrigger);
        }
        else
        {
            Debug.LogWarning($"[PlayerMeleeCombat] Trigger vazio para ataque {type}.");
        }
    }

    private void Update()
{
        // Botão esquerdo do mouse
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            
            TryAttack(AttackType.Straight);
        }
}

    #endregion

    #region Animation Events

    /// <summary>
    /// Chame este método via Animation Event no frame de impacto do ataque Straight.
    /// </summary>
    public void AE_Hit_Straight()
    {
        PerformHit(AttackType.Straight);
    }

    /// <summary>
    /// Chame este método via Animation Event no frame de impacto do ataque Overhead.
    /// </summary>
    public void AE_Hit_Overhead()
    {
        PerformHit(AttackType.Overhead);
    }

    /// <summary>
    /// Chame este método via Animation Event no frame de impacto do ataque Uppercut.
    /// </summary>
    public void AE_Hit_Uppercut()
    {
        PerformHit(AttackType.Uppercut);
    }

    #endregion

    private void PerformHit(AttackType type)
    {
        if (playerCamera == null)
        {
            Debug.LogWarning("[PlayerMeleeCombat] playerCamera não atribuída.");
            return;
        }

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

            // Dano: tenta interface primeiro (recomendado)
            var damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(data.damage);
            }
            else
            {
                // Fallback opcional: SendMessage (caso seu inimigo tenha TakeDamage(float))
                col.transform.root.SendMessage("TakeDamage", data.damage, SendMessageOptions.DontRequireReceiver);
            }

            // Knockback
            Rigidbody rb = col.attachedRigidbody;
            if (rb != null && !rb.isKinematic)
            {
                Vector3 forceDir = GetKnockbackDirection(type);
                rb.AddForce(forceDir * data.knockbackForce, ForceMode.Impulse);
            }

            _hitBuffer[i] = null; // limpa referência do buffer
        }
    }

    private Vector3 GetWorldCenter(Vector3 localOffset)
    {
        // Offset em espaço local da câmera => mundo
        return playerCamera.transform.TransformPoint(localOffset);
    }

    private Vector3 GetKnockbackDirection(AttackType type)
    {
        Vector3 forward = playerCamera.transform.forward;
        Vector3 up = Vector3.up;

        switch (type)
        {
            case AttackType.Uppercut:
                // Uppercut: para frente + para cima
                return (forward + up * 0.9f).normalized;

            case AttackType.Overhead:
                // Leve tendência para baixo/frente (ajuste opcional)
                return (forward + Vector3.down * 0.15f).normalized;

            case AttackType.Straight:
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

        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
        Gizmos.DrawWireCube(Vector3.zero, data.halfExtents * 2f);

        Gizmos.matrix = old;
    }
}

/// <summary>
/// Interface opcional para receber dano de forma limpa.
/// Seus inimigos podem implementar isso.
/// </summary>
public interface IDamageable
{
    void TakeDamage(float amount);
}