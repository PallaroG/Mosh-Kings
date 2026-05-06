using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyPushDamage : MonoBehaviour
{
    [Header("Dano por empurrão")]
    [SerializeField] private float basePushDamage = 8f;      // configurável por inimigo
    [SerializeField] private float impactMultiplier = 0.15f; // escala com impacto
    [SerializeField] private PushPreset pushPreset = PushPreset.Custom;
    [SerializeField] private float pushForce = 6f;           // força aplicada no player
    [SerializeField] private float impactPushMultiplier = 0.08f;
    [SerializeField] private float maxImpactPushBonus = 3f;
    [SerializeField] private float hitCooldown = 0.35f;      // evita dano por frame

    private float lastHitTime = -999f;

    private void OnCollisionStay(Collision collision)
    {
        TryApplyPushAndDamage(collision.collider, collision.relativeVelocity);
    }

    private void OnTriggerStay(Collider other)
    {
        // Se usa trigger, impacto físico real não vem pronto.
        // Aqui usa dano base (impacto = 0), mas ainda aplica empurrão.
        Vector3 estimatedImpact = Vector3.zero;
        TryApplyPushAndDamage(other, estimatedImpact);
    }

    private void TryApplyPushAndDamage(Collider other, Vector3 relativeVelocity)
    {
        if (Time.time - lastHitTime < hitCooldown) return;

        PlayerStamina stamina = other.GetComponent<PlayerStamina>();
        if (stamina == null) stamina = other.GetComponentInParent<PlayerStamina>();
        if (stamina == null) return;

        MovementPlayer movement = other.GetComponent<MovementPlayer>();
        if (movement == null) movement = other.GetComponentInParent<MovementPlayer>();

        // Direção do empurrão (inimigo -> player)
        Vector3 dir = (other.transform.position - transform.position).normalized;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;

        // Intensidade de impacto
        float impact = relativeVelocity.magnitude;
        float finalDamage = basePushDamage + (impact * impactMultiplier);

        // Aplica dano de cansaço
        stamina.ReceivePushDamage(finalDamage);

        // Empurrão no CharacterController via velocidade externa
        if (movement != null)
        {
            movement.AddPush(dir, GetPushForce(movement, impact));
        }

        lastHitTime = Time.time;
    }

    private float GetPushForce(MovementPlayer movement, float impact)
    {
        float baseForce = pushPreset == PushPreset.Custom || movement == null
            ? pushForce
            : movement.GetPushForce(pushPreset);

        float impactBonus = Mathf.Min(impact * impactPushMultiplier, maxImpactPushBonus);
        return baseForce + impactBonus;
    }
}
