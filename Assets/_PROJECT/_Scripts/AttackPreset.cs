using UnityEngine;

public enum AttackPresetType
{
    SimplePunch,
    AreaPush,
    GroundSlam,
    WallPin,
    Grab,
    Stun,
    Projectile
}

[CreateAssetMenu(fileName = "AttackPreset", menuName = "Mosh Kings/Combat/Attack Preset")]
public class AttackPreset : ScriptableObject
{
    [Header("Identidade")]
    [Tooltip("Nome curto do ataque para identificacao no Inspector.")]
    [SerializeField] private string attackName = "Novo Ataque";
    [Tooltip("Descricao de uso do ataque. Exemplo: soco rapido, empurrao em area, golpe pesado.")]
    [TextArea(2, 4)]
    [SerializeField] private string description;
    [Tooltip("Tipo geral do ataque. Nesta fase apenas SimplePunch e AreaPush sao dados; tipos futuros ainda nao tem logica propria.")]
    [SerializeField] private AttackPresetType attackType = AttackPresetType.SimplePunch;

    [Header("Animacao")]
    [Tooltip("Trigger enviado para o Animator atual.")]
    [SerializeField] private string animationTrigger;

    [Header("Dano e Empurrao")]
    [Tooltip("Dano de stamina/cansaco causado por este ataque.")]
    [SerializeField] private float damage = 10f;
    [Tooltip("Preset de empurrao usado por este ataque. Se vazio, o PlayerMeleeCombat pode usar o PushPreset legacy do AttackData.")]
    [SerializeField] private PushPreset pushPreset;

    [Header("Hitbox")]
    [Tooltip("Metade do tamanho da caixa de acerto. Aumente para facilitar acertar alvos.")]
    [SerializeField] private Vector3 halfExtents = new Vector3(0.45f, 0.45f, 0.8f);
    [Tooltip("Offset local da hitbox a partir da camera do player.")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0f, 1f);
    [Tooltip("Se ligado, cada collider so pode ser acertado uma vez por swing.")]
    [SerializeField] private bool singleHitPerSwing = true;

    [Header("Timing")]
    [Tooltip("Tempo minimo antes de iniciar outro ataque deste tipo.")]
    [SerializeField] private float cooldown = 0.35f;
    [Tooltip("Tempo de recuperacao do golpe. Ainda e apenas dado para a proxima fase.")]
    [SerializeField] private float recoveryTime = 0.2f;
    [Tooltip("Janela futura de combo. Ainda nao executa combo nesta fase.")]
    [SerializeField] private float comboWindow = 0.35f;
    [Tooltip("Dado futuro: se ligado, o combo so deve continuar se este ataque acertar.")]
    [SerializeField] private bool requireHitToContinueCombo = false;

    [Header("Debug")]
    [Tooltip("Cor opcional para visualizar a hitbox deste ataque.")]
    [SerializeField] private Color debugColor = new Color(1f, 0.25f, 0f, 0.35f);

    public string AttackName => attackName;
    public string Description => description;
    public AttackPresetType AttackType => attackType;
    public string AnimationTrigger => animationTrigger;
    public float Damage => damage;
    public PushPreset PushPreset => pushPreset;
    public Vector3 HalfExtents => halfExtents;
    public Vector3 LocalOffset => localOffset;
    public bool SingleHitPerSwing => singleHitPerSwing;
    public float Cooldown => cooldown;
    public float RecoveryTime => recoveryTime;
    public float ComboWindow => comboWindow;
    public bool RequireHitToContinueCombo => requireHitToContinueCombo;
    public Color DebugColor => debugColor;

    public ImpactData CreateImpactData(float resolvedDamage, Vector3 direction, Vector3 sourcePosition)
    {
        if (pushPreset != null)
            return pushPreset.CreateImpactData(resolvedDamage, direction, sourcePosition);

        return new ImpactData(
            resolvedDamage,
            direction,
            0f,
            0f,
            0f,
            0f,
            sourcePosition);
    }
}
