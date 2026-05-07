using UnityEngine;
using Unity.Netcode;
using UnityEngine.Serialization;
using System;

public enum LegacyPushPreset
{
    Custom,
    Light,
    Medium,
    Heavy
}

public enum WallImpactMode
{
    Stop,
    Slide,
    Impact
}

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkObject))]
public class MovementPlayer : NetworkBehaviour, IImpactReceiver
{
    [Header("Referências")]
    [SerializeField] private Transform cameraRoot;

    [Header("Movimento")]
    [SerializeField] private float walkSpeed = 4.5f;
    [SerializeField] private float sprintSpeed = 7.5f;
    [SerializeField] private float inputResponsiveness = 55f;
    [SerializeField] private float stopResponsiveness = 85f;
    [SerializeField] private float directionChangeResponsiveness = 95f;
    [SerializeField] private float sprintRampTime = 0.12f;
    [SerializeField] private float airControlPercent = 0.35f;

    [Header("Pulo / Gravidade")]
    [SerializeField] private bool jumpEnabled = true;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float groundedStickForce = -2f;

    [Header("Forças Externas (Empurrão)")]
    [Tooltip("Rapidez com que a velocidade externa acompanha o alvo do impacto. Alto = tranco mais seco.")]
    [SerializeField] private float externalAcceleration = 42f;
    [Tooltip("Desaceleracao padrao do empurrao quando o impacto nao define duracao. Alto = empurrao mais curto.")]
    [SerializeField] private float externalDeceleration = 16f;
    [Tooltip("Limite padrao da velocidade externa causada por empurroes.")]
    [SerializeField] private float maxExternalSpeed = 13f;
    [Tooltip("Parte da forca aplicada imediatamente. Alto = pancada mais seca; baixo = mais deslize.")]
    [SerializeField, Range(0f, 1f)] private float immediatePushPercent = 0.55f;
    [FormerlySerializedAs("minControlWhilePushed")]
    [Tooltip("Controle minimo restante durante impactos fortes. 1 = controle total, 0 = sem controle.")]
    [SerializeField, Range(0f, 1f)] private float pushControlMultiplier = 0.35f;
    [Tooltip("Velocidade com que o controle do player volta ao normal depois do empurrao.")]
    [SerializeField] private float controlRecoverySpeed = 4.5f;
    [Tooltip("Velocidade externa minima para ainda reduzir o controle do player.")]
    [SerializeField] private float pushedControlThreshold = 0.35f;
    [Tooltip("Abaixo desse valor, a velocidade externa residual e zerada.")]
    [SerializeField] private float externalStopThreshold = 0.05f;

    [Header("Impacto em Parede")]
    [Tooltip("Stop corta o empurrao lateral. Slide remove a forca contra a parede e preserva parte do deslize. Impact corta apenas batidas fortes.")]
    [SerializeField] private WallImpactMode wallImpactMode = WallImpactMode.Slide;
    [Tooltip("Quanto da forca externa e perdida ao bater em parede. 1 corta tudo, 0 preserva tudo que puder deslizar.")]
    [SerializeField, Range(0f, 1f)] private float wallForceLossMultiplier = 0.65f;
    [Tooltip("Velocidade externa minima contra a parede para contar como impacto forte.")]
    [SerializeField] private float strongWallImpactSpeed = 6f;
    [Tooltip("Se ligado, reduz temporariamente o controle do jogador ao bater forte na parede.")]
    [SerializeField] private bool reduceControlOnWallImpact = true;
    [Tooltip("Controle restante depois de impacto forte na parede.")]
    [SerializeField, Range(0f, 1f)] private float wallImpactControlMultiplier = 0.2f;
    [Tooltip("Tempo minimo entre disparos de wall impact para evitar repeticao por varios contatos no mesmo empurrao.")]
    [SerializeField] private float wallImpactCooldown = 0.12f;
    [Tooltip("Exibe logs simples quando uma batida forte em parede for detectada.")]
    [SerializeField] private bool debugWallImpactLogs = false;

    [Header("Presets de Empurrão")]
    [SerializeField] private float lightPushForce = 4f;
    [SerializeField] private float mediumPushForce = 7.5f;
    [SerializeField] private float heavyPushForce = 12f;

    [Header("Input")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    private CharacterController controller;
    private Vector3 horizontalVelocity;
    private float sprintBlend;
    private float verticalVelocity;

    private Vector3 externalVelocity;
    private Vector3 externalTargetVelocity;
    private float inputControlMultiplier = 1f;
    private float activeImpactDeceleration;
    private float activeExternalDeceleration;
    private WallImpactMode activeWallImpactMode;
    private float activeWallForceLossMultiplier;
    private float activeStrongWallImpactSpeed;
    private bool activeReduceControlOnWallImpact;
    private float activeWallImpactControlMultiplier;
    private float lastWallImpactTime = -999f;

    public event Action<Vector3, float> OnWallImpact;

    // Sincronização básica de transform (caso você não esteja usando NetworkTransform)
    private NetworkVariable<Vector3> netPosition = new(
        Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<Quaternion> netRotation = new(
        Quaternion.identity, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool JumpEnabled
    {
        get => jumpEnabled;
        set => jumpEnabled = value;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        ResetActiveWallImpactSettings();

        if (cameraRoot == null && Camera.main != null)
            cameraRoot = Camera.main.transform;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            netPosition.Value = transform.position;
            netRotation.Value = transform.rotation;
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsOwner)
        {
            HandleMovementLocal();
            SendTransformToServerRpc(transform.position, transform.rotation);
        }
        else
        {
            // Interpolação simples para remotos
            transform.position = Vector3.Lerp(transform.position, netPosition.Value, 15f * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, netRotation.Value, 15f * Time.deltaTime);
        }
    }

    public void AddPush(Vector3 direction, float force)
    {
        if (direction.sqrMagnitude < 0.0001f || force <= 0f) return;

        ImpactData impact = CreateDefaultImpact(direction, force);

        if (IsServer)
        {
            ApplyImpactLocal(impact);
            SendImpactToOwnerClient(impact);
        }
        else
        {
            if (IsOwner)
            {
                ApplyImpactLocal(impact);
                AddPushServerRpc(direction, force);
            }
        }
    }

    public void ReceiveImpact(ImpactData impact)
    {
        if (impact.direction.sqrMagnitude < 0.0001f && impact.sourcePosition != Vector3.zero)
            impact.direction = transform.position - impact.sourcePosition;

        if (impact.direction.sqrMagnitude < 0.0001f || impact.force <= 0f) return;

        if (IsServer)
        {
            ApplyImpactLocal(impact);
            SendImpactToOwnerClient(impact);
        }
        else if (IsOwner)
        {
            ApplyImpactLocal(impact);
            ReceiveImpactServerRpc(impact);
        }
    }

    public void AddPush(Vector3 direction, PushPreset preset)
    {
        if (preset == null) return;
        ReceiveImpact(preset.CreateImpactData(0f, direction));
    }

    public void AddPush(Vector3 direction, LegacyPushPreset preset)
    {
        AddPush(direction, GetPushForce(preset));
    }

    public float GetPushForce(LegacyPushPreset preset)
    {
        return preset switch
        {
            LegacyPushPreset.Light => lightPushForce,
            LegacyPushPreset.Heavy => heavyPushForce,
            _ => mediumPushForce
        };
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddPushServerRpc(Vector3 direction, float force, ServerRpcParams rpcParams = default)
    {
        ImpactData impact = CreateDefaultImpact(direction, force);
        ApplyImpactLocal(impact);

        // Future server validation should clamp/approve direction and force here before echoing.
        SendImpactToOwnerClient(impact, rpcParams.Receive.SenderClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReceiveImpactServerRpc(ImpactData impact, ServerRpcParams rpcParams = default)
    {
        ApplyImpactLocal(impact);
        SendImpactToOwnerClient(impact, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    private void ApplyImpactClientRpc(ImpactData impact, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        ApplyImpactLocal(impact);
    }

    private void SendImpactToOwnerClient(ImpactData impact, ulong skipClientId = ulong.MaxValue)
    {
        if (!IsSpawned) return;
        if (OwnerClientId == NetworkManager.ServerClientId) return;
        if (OwnerClientId == skipClientId) return;

        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { OwnerClientId }
            }
        };

        ApplyImpactClientRpc(impact, clientRpcParams);
    }

    private ImpactData CreateDefaultImpact(Vector3 direction, float force)
    {
        return new ImpactData(
            0f,
            direction,
            force,
            0f,
            maxExternalSpeed,
            pushControlMultiplier);
    }

    private void ApplyImpactLocal(ImpactData impact)
    {
        Vector3 direction = impact.direction;
        float force = impact.force;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        direction.Normalize();
        float speedLimit = impact.maxExternalSpeed > 0f ? impact.maxExternalSpeed : maxExternalSpeed;
        float targetControl = impact.targetControlMultiplier > 0f
            ? Mathf.Clamp01(impact.targetControlMultiplier)
            : pushControlMultiplier;

        Vector3 impulse = direction * force;
        Vector3 immediateVelocity = impulse * immediatePushPercent;
        Vector3 glideVelocity = impulse * (1f - immediatePushPercent);

        externalVelocity += immediateVelocity;
        externalTargetVelocity += glideVelocity;
        externalVelocity = Vector3.ClampMagnitude(externalVelocity, speedLimit);
        externalTargetVelocity = Vector3.ClampMagnitude(externalTargetVelocity, speedLimit);

        if (impact.duration > 0.01f)
            activeImpactDeceleration = Mathf.Max(activeImpactDeceleration, force / impact.duration);

        if (impact.externalDeceleration > 0f)
            activeExternalDeceleration = Mathf.Max(activeExternalDeceleration, impact.externalDeceleration);

        if (impact.useWallImpactSettings)
        {
            activeWallImpactMode = impact.wallImpactMode;
            activeWallForceLossMultiplier = Mathf.Clamp01(impact.wallForceLossMultiplier);
            activeStrongWallImpactSpeed = impact.strongWallImpactSpeed > 0f
                ? impact.strongWallImpactSpeed
                : strongWallImpactSpeed;
            activeReduceControlOnWallImpact = impact.reduceControlOnWallImpact;
            activeWallImpactControlMultiplier = impact.wallImpactControlMultiplier >= 0f
                ? Mathf.Clamp01(impact.wallImpactControlMultiplier)
                : wallImpactControlMultiplier;
        }

        float forcePercent = Mathf.Clamp01(force / Mathf.Max(speedLimit, 0.01f));
        float pushedControl = Mathf.Lerp(1f, targetControl, forcePercent);
        inputControlMultiplier = Mathf.Min(inputControlMultiplier, pushedControl);
    }

    [ServerRpc]
    private void SendTransformToServerRpc(Vector3 pos, Quaternion rot)
    {
        netPosition.Value = pos;
        netRotation.Value = rot;
    }

    private void HandleMovementLocal()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector2 input = Vector2.ClampMagnitude(new Vector2(x, z), 1f);

        Vector3 forward = cameraRoot != null ? cameraRoot.forward : transform.forward;
        Vector3 right = cameraRoot != null ? cameraRoot.right : transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        RecoverInputControl();
        UpdateExternalVelocity();

        Vector3 inputDirection = GetInputDirection(input, forward, right);
        bool hasInput = inputDirection.sqrMagnitude > 0.0001f;
        float targetSpeed = GetSprintAdjustedSpeed(hasInput);
        float control = (controller.isGrounded ? 1f : airControlPercent) * GetCurrentInputControl();

        UpdateInputVelocity(inputDirection, targetSpeed, control);
        UpdateGravity();

        Vector3 finalVelocity = horizontalVelocity + externalVelocity;
        finalVelocity.y = verticalVelocity;

        controller.Move(finalVelocity * Time.deltaTime);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!IsOwner) return;
        if (hit == null) return;
        if (externalVelocity.sqrMagnitude <= externalStopThreshold * externalStopThreshold) return;

        Vector3 wallNormal = hit.normal;
        if (wallNormal.y > 0.35f) return;

        wallNormal.y = 0f;
        if (wallNormal.sqrMagnitude < 0.0001f) return;

        wallNormal.Normalize();
        float intoWallSpeed = -Vector3.Dot(externalVelocity, wallNormal);
        if (intoWallSpeed <= externalStopThreshold) return;

        bool strongImpact = intoWallSpeed >= activeStrongWallImpactSpeed;
        ResolveExternalVelocityAgainstWall(wallNormal, strongImpact);

        if (!strongImpact || Time.time - lastWallImpactTime < wallImpactCooldown) return;

        lastWallImpactTime = Time.time;

        if (activeReduceControlOnWallImpact)
            inputControlMultiplier = Mathf.Min(inputControlMultiplier, activeWallImpactControlMultiplier);

        // Future feedback and optional stamina damage can hook into this event after Phase 1.
        OnWallImpact?.Invoke(wallNormal, intoWallSpeed);

        if (debugWallImpactLogs)
            Debug.Log($"[MovementPlayer] Wall impact speed: {intoWallSpeed:0.00}", this);
    }

    private void ResolveExternalVelocityAgainstWall(Vector3 wallNormal, bool strongImpact)
    {
        float retainedForce = 1f - activeWallForceLossMultiplier;
        Vector3 slidVelocity = Vector3.ProjectOnPlane(externalVelocity, wallNormal);
        Vector3 slidTargetVelocity = Vector3.ProjectOnPlane(externalTargetVelocity, wallNormal);

        switch (activeWallImpactMode)
        {
            case WallImpactMode.Stop:
                externalVelocity = Vector3.zero;
                externalTargetVelocity = Vector3.zero;
                break;

            case WallImpactMode.Impact:
                if (strongImpact)
                {
                    externalVelocity = Vector3.zero;
                    externalTargetVelocity = Vector3.zero;
                }
                else
                {
                    externalVelocity = slidVelocity * retainedForce;
                    externalTargetVelocity = slidTargetVelocity * retainedForce;
                }
                break;

            default:
                externalVelocity = slidVelocity * retainedForce;
                externalTargetVelocity = slidTargetVelocity * retainedForce;
                break;
        }

        float wallDeceleration = externalDeceleration * Mathf.Lerp(1f, 5f, activeWallForceLossMultiplier);
        activeImpactDeceleration = Mathf.Max(activeImpactDeceleration, wallDeceleration);
    }

    private Vector3 GetInputDirection(Vector2 input, Vector3 forward, Vector3 right)
    {
        Vector3 desiredMove = forward * input.y + right * input.x;
        desiredMove.y = 0f;

        if (desiredMove.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return desiredMove.normalized;
    }

    private float GetSprintAdjustedSpeed(bool hasInput)
    {
        bool wantsSprint = hasInput && Input.GetKey(sprintKey);
        float rampTime = Mathf.Max(sprintRampTime, 0.001f);
        float sprintTarget = wantsSprint ? 1f : 0f;
        float sprintRate = (wantsSprint ? 1f / rampTime : 12f) * Time.deltaTime;

        sprintBlend = Mathf.MoveTowards(sprintBlend, sprintTarget, sprintRate);
        return Mathf.Lerp(walkSpeed, sprintSpeed, sprintBlend);
    }

    private void UpdateInputVelocity(Vector3 inputDirection, float targetSpeed, float control)
    {
        if (inputDirection.sqrMagnitude < 0.0001f)
        {
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                Vector3.zero,
                stopResponsiveness * control * Time.deltaTime);
            return;
        }

        Vector3 targetVelocity = inputDirection * targetSpeed;
        float responsiveness = inputResponsiveness;

        if (horizontalVelocity.sqrMagnitude > 0.0001f)
        {
            float directionDot = Vector3.Dot(horizontalVelocity.normalized, inputDirection);
            if (directionDot < 0.65f)
                responsiveness = directionChangeResponsiveness;
        }

        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetVelocity,
            responsiveness * control * Time.deltaTime);
    }

    private void UpdateGravity()
    {
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = groundedStickForce;

            if (jumpEnabled && Input.GetKeyDown(jumpKey))
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;
    }

    private void UpdateExternalVelocity()
    {
        float deceleration = Mathf.Max(externalDeceleration, activeImpactDeceleration, activeExternalDeceleration);
        externalTargetVelocity = Vector3.MoveTowards(externalTargetVelocity, Vector3.zero, deceleration * Time.deltaTime);
        externalVelocity = Vector3.MoveTowards(externalVelocity, externalTargetVelocity, externalAcceleration * Time.deltaTime);

        if (externalVelocity.sqrMagnitude <= externalStopThreshold * externalStopThreshold)
            externalVelocity = Vector3.zero;

        if (externalTargetVelocity.sqrMagnitude <= externalStopThreshold * externalStopThreshold)
            externalTargetVelocity = Vector3.zero;

        activeImpactDeceleration = Mathf.MoveTowards(activeImpactDeceleration, 0f, externalDeceleration * Time.deltaTime);
        activeExternalDeceleration = Mathf.MoveTowards(activeExternalDeceleration, 0f, externalDeceleration * Time.deltaTime);

        if (externalVelocity == Vector3.zero && externalTargetVelocity == Vector3.zero)
            ResetActiveWallImpactSettings();
    }

    private void ResetActiveWallImpactSettings()
    {
        activeWallImpactMode = wallImpactMode;
        activeWallForceLossMultiplier = wallForceLossMultiplier;
        activeStrongWallImpactSpeed = strongWallImpactSpeed;
        activeReduceControlOnWallImpact = reduceControlOnWallImpact;
        activeWallImpactControlMultiplier = wallImpactControlMultiplier;
    }

    private void RecoverInputControl()
    {
        inputControlMultiplier = Mathf.MoveTowards(inputControlMultiplier, 1f, controlRecoverySpeed * Time.deltaTime);
    }

    private float GetCurrentInputControl()
    {
        if (externalVelocity.magnitude < pushedControlThreshold)
            return 1f;

        return inputControlMultiplier;
    }
}
