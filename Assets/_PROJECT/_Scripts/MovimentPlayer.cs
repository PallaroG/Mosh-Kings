using UnityEngine;
using Unity.Netcode;
using UnityEngine.Serialization;

public enum PushPreset
{
    Custom,
    Light,
    Medium,
    Heavy
}

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkObject))]
public class MovementPlayer : NetworkBehaviour
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
    [SerializeField] private float externalAcceleration = 42f;
    [SerializeField] private float externalDeceleration = 16f;
    [SerializeField] private float maxExternalSpeed = 13f;
    [SerializeField, Range(0f, 1f)] private float immediatePushPercent = 0.55f;
    [FormerlySerializedAs("minControlWhilePushed")]
    [SerializeField, Range(0f, 1f)] private float pushControlMultiplier = 0.35f;
    [SerializeField] private float controlRecoverySpeed = 4.5f;
    [SerializeField] private float pushedControlThreshold = 0.35f;
    [SerializeField] private float externalStopThreshold = 0.05f;

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

        if (IsServer)
        {
            ApplyPushLocal(direction, force);
            SendPushToOwnerClient(direction, force);
        }
        else
        {
            if (IsOwner)
            {
                ApplyPushLocal(direction, force);
                AddPushServerRpc(direction, force);
            }
        }
    }

    public void AddPush(Vector3 direction, PushPreset preset)
    {
        AddPush(direction, GetPushForce(preset));
    }

    public float GetPushForce(PushPreset preset)
    {
        return preset switch
        {
            PushPreset.Light => lightPushForce,
            PushPreset.Heavy => heavyPushForce,
            _ => mediumPushForce
        };
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddPushServerRpc(Vector3 direction, float force, ServerRpcParams rpcParams = default)
    {
        ApplyPushLocal(direction, force);

        // Future server validation should clamp/approve direction and force here before echoing.
        SendPushToOwnerClient(direction, force, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    private void ApplyPushClientRpc(Vector3 direction, float force, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner) return;
        ApplyPushLocal(direction, force);
    }

    private void SendPushToOwnerClient(Vector3 direction, float force, ulong skipClientId = ulong.MaxValue)
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

        ApplyPushClientRpc(direction, force, clientRpcParams);
    }

    private void ApplyPushLocal(Vector3 direction, float force)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        direction.Normalize();
        Vector3 impulse = direction * force;
        Vector3 immediateVelocity = impulse * immediatePushPercent;
        Vector3 glideVelocity = impulse * (1f - immediatePushPercent);

        externalVelocity += immediateVelocity;
        externalTargetVelocity += glideVelocity;
        externalVelocity = Vector3.ClampMagnitude(externalVelocity, maxExternalSpeed);
        externalTargetVelocity = Vector3.ClampMagnitude(externalTargetVelocity, maxExternalSpeed);

        float forcePercent = Mathf.Clamp01(force / Mathf.Max(maxExternalSpeed, 0.01f));
        float pushedControl = Mathf.Lerp(1f, pushControlMultiplier, forcePercent);
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
        externalTargetVelocity = Vector3.MoveTowards(externalTargetVelocity, Vector3.zero, externalDeceleration * Time.deltaTime);
        externalVelocity = Vector3.MoveTowards(externalVelocity, externalTargetVelocity, externalAcceleration * Time.deltaTime);

        if (externalVelocity.sqrMagnitude <= externalStopThreshold * externalStopThreshold)
            externalVelocity = Vector3.zero;

        if (externalTargetVelocity.sqrMagnitude <= externalStopThreshold * externalStopThreshold)
            externalTargetVelocity = Vector3.zero;
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
