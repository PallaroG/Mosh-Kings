using UnityEngine;

[CreateAssetMenu(fileName = "PushPreset", menuName = "Mosh Kings/Combat/Push Preset")]
public class PushPreset : ScriptableObject
{
    [Header("Identidade")]
    [Tooltip("Nome curto para identificar este empurrao no Inspector.")]
    [SerializeField] private string presetName = "Novo Push";
    [Tooltip("Descricao de uso. Exemplo: soco leve, ombrada forte, explosao curta.")]
    [TextArea(2, 4)]
    [SerializeField] private string description;

    [Header("Dano de Stamina")]
    [Tooltip("Multiplica o dano de stamina informado pela fonte. 1 mantem o dano original.")]
    [SerializeField, Range(0f, 3f)] private float staminaDamageMultiplier = 1f;

    [Header("Empurrao")]
    [Tooltip("Forca horizontal do empurrao. Aumente para jogar o alvo mais longe.")]
    [SerializeField] private float horizontalForce = 7.5f;
    [Tooltip("Duracao desejada do impacto. Valores menores deixam o empurrao mais seco.")]
    [SerializeField] private float impactDuration = 0.25f;
    [Tooltip("Limite de velocidade externa. Aumente para permitir empurroes mais rapidos.")]
    [SerializeField] private float maxExternalSpeed = 13f;
    [Tooltip("Desaceleracao especifica deste empurrao. Aumente para parar mais rapido; reduza para escorregar mais.")]
    [SerializeField] private float externalDeceleration = 16f;
    [Tooltip("Controle restante do alvo durante o impacto. 1 = controle total, 0 = sem controle.")]
    [SerializeField, Range(0f, 1f)] private float targetControlMultiplier = 0.35f;

    [Header("Parede")]
    [Tooltip("Stop corta ao bater, Slide remove a forca contra a parede, Impact corta apenas batidas fortes.")]
    [SerializeField] private WallImpactMode wallImpactMode = WallImpactMode.Slide;
    [Tooltip("Perda de forca ao bater na parede. 1 para muito, 0 deixa deslizar mais.")]
    [SerializeField, Range(0f, 1f)] private float wallForceLossMultiplier = 0.65f;
    [Tooltip("Velocidade minima contra a parede para contar como wall impact forte.")]
    [SerializeField] private float strongWallImpactSpeed = 6f;
    [Tooltip("Reduz temporariamente o controle do alvo quando houver wall impact forte.")]
    [SerializeField] private bool reduceControlOnWallImpact = true;
    [Tooltip("Controle restante depois de bater forte na parede.")]
    [SerializeField, Range(0f, 1f)] private float wallImpactControlMultiplier = 0.2f;

    public string PresetName => presetName;
    public string Description => description;
    public float StaminaDamageMultiplier => staminaDamageMultiplier;
    public float HorizontalForce => horizontalForce;
    public float ImpactDuration => impactDuration;
    public float MaxExternalSpeed => maxExternalSpeed;
    public float ExternalDeceleration => externalDeceleration;
    public float TargetControlMultiplier => targetControlMultiplier;
    public WallImpactMode WallImpactMode => wallImpactMode;
    public float WallForceLossMultiplier => wallForceLossMultiplier;
    public float StrongWallImpactSpeed => strongWallImpactSpeed;
    public bool ReduceControlOnWallImpact => reduceControlOnWallImpact;
    public float WallImpactControlMultiplier => wallImpactControlMultiplier;

    public float ResolveDamage(float baseDamage)
    {
        return Mathf.Max(0f, baseDamage * staminaDamageMultiplier);
    }

    public ImpactData CreateImpactData(float baseDamage, Vector3 direction, Vector3 sourcePosition = default)
    {
        return new ImpactData(
            ResolveDamage(baseDamage),
            direction,
            horizontalForce,
            impactDuration,
            maxExternalSpeed,
            targetControlMultiplier,
            sourcePosition,
            externalDeceleration,
            true,
            wallImpactMode,
            wallForceLossMultiplier,
            strongWallImpactSpeed,
            reduceControlOnWallImpact,
            wallImpactControlMultiplier);
    }
}
