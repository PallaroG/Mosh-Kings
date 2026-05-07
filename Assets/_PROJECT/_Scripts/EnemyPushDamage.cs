using UnityEngine;
using UnityEngine.Serialization;

[RequireComponent(typeof(Collider))]
public class EnemyPushDamage : MonoBehaviour
{
    [Header("Dano por empurrao")]
    [SerializeField] private float basePushDamage = 8f;
    [SerializeField] private float impactMultiplier = 0.15f;
    [Tooltip("Preset novo de empurrao. Se preenchido, define forca, duracao, controle e comportamento de parede.")]
    [SerializeField] private PushPreset pushPresetAsset;
    [Header("Legacy Push")]
    [FormerlySerializedAs("pushPreset")]
    [SerializeField] private LegacyPushPreset legacyPushPreset = LegacyPushPreset.Custom;
    [Tooltip("Forca antiga de empurrao. Usada quando PushPreset novo nao esta atribuido.")]
    [SerializeField] private float pushForce = 6f;
    [SerializeField] private float impactDuration = 0f;
    [SerializeField] private float maxExternalSpeed = 0f;
    [SerializeField, Range(0f, 1f)] private float targetControlMultiplier = 0.35f;
    [SerializeField] private float impactPushMultiplier = 0.08f;
    [SerializeField] private float maxImpactPushBonus = 3f;
    [SerializeField] private float hitCooldown = 0.35f;

    private float lastHitTime = -999f;

    private void OnCollisionStay(Collision collision)
    {
        TryApplyPushAndDamage(collision.collider, collision.relativeVelocity);
    }

    private void OnTriggerStay(Collider other)
    {
        TryApplyPushAndDamage(other, Vector3.zero);
    }

    private void TryApplyPushAndDamage(Collider other, Vector3 relativeVelocity)
    {
        if (Time.time - lastHitTime < hitCooldown) return;

        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable == null) damageable = other.GetComponentInParent<IDamageable>();

        IImpactReceiver impactReceiver = other.GetComponent<IImpactReceiver>();
        if (impactReceiver == null) impactReceiver = other.GetComponentInParent<IImpactReceiver>();

        if (damageable == null && impactReceiver == null) return;

        Vector3 direction = other.transform.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;

        float impact = relativeVelocity.magnitude;
        float finalDamage = basePushDamage + impact * impactMultiplier;
        ImpactData impactData = default;
        bool hasImpactData = impactReceiver != null;

        if (hasImpactData)
        {
            MovementPlayer movement = impactReceiver as MovementPlayer;
            impactData = CreateImpactData(movement, finalDamage, direction, impact);
            finalDamage = impactData.damage;
        }
        else if (pushPresetAsset != null)
        {
            finalDamage = pushPresetAsset.ResolveDamage(finalDamage);
        }

        if (damageable != null)
            damageable.TakeDamage(finalDamage);

        if (hasImpactData)
            impactReceiver.ReceiveImpact(impactData);

        lastHitTime = Time.time;
    }

    private ImpactData CreateImpactData(MovementPlayer movement, float finalDamage, Vector3 direction, float impact)
    {
        if (pushPresetAsset != null)
        {
            ImpactData data = pushPresetAsset.CreateImpactData(finalDamage, direction, transform.position);
            data.force += GetImpactPushBonus(impact);
            return data;
        }

        return new ImpactData(
            finalDamage,
            direction,
            GetLegacyPushForce(movement, impact),
            impactDuration,
            maxExternalSpeed,
            targetControlMultiplier,
            transform.position);
    }

    private float GetLegacyPushForce(MovementPlayer movement, float impact)
    {
        float baseForce = legacyPushPreset == LegacyPushPreset.Custom || movement == null
            ? pushForce
            : movement.GetPushForce(legacyPushPreset);

        return baseForce + GetImpactPushBonus(impact);
    }

    private float GetImpactPushBonus(float impact)
    {
        float impactBonus = Mathf.Min(impact * impactPushMultiplier, maxImpactPushBonus);
        return impactBonus;
    }
}
