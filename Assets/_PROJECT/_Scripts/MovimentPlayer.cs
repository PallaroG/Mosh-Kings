using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkObject))]
public class MovementPlayer : NetworkBehaviour
{
    [Header("Referências")]
    [SerializeField] private Transform cameraRoot;

    [Header("Movimento")]
    [SerializeField] private float walkSpeed = 4.5f;
    [SerializeField] private float sprintSpeed = 7.5f;
    [SerializeField] private float acceleration = 14f;
    [SerializeField] private float deceleration = 18f;
    [SerializeField] private float airControlPercent = 0.35f;

    [Header("Pulo / Gravidade")]
    [SerializeField] private bool jumpEnabled = true;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float groundedStickForce = -2f;

    [Header("Forças Externas (Empurrão)")]
    [SerializeField] private float externalAcceleration = 30f;
    [SerializeField] private float externalDeceleration = 20f;
    [SerializeField] private float maxExternalSpeed = 10f;

    [Header("Input")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    private CharacterController controller;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;

    private Vector3 externalVelocity;
    private Vector3 externalTargetVelocity;

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
            ApplyPush(direction, force);
        }
        else
        {
            AddPushServerRpc(direction, force);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddPushServerRpc(Vector3 direction, float force)
    {
        ApplyPush(direction, force);
    }

    private void ApplyPush(Vector3 direction, float force)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        direction.Normalize();
        externalTargetVelocity += direction * force;
        externalTargetVelocity = Vector3.ClampMagnitude(externalTargetVelocity, maxExternalSpeed);
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

        Vector3 desiredMove = (forward * input.y + right * input.x);

        bool isSprinting = Input.GetKey(sprintKey);
        float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        Vector3 targetVelocity = desiredMove * targetSpeed;

        float control = controller.isGrounded ? 1f : airControlPercent;
        float accelRate = (targetVelocity.sqrMagnitude > 0.01f) ? acceleration : deceleration;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, accelRate * control * Time.deltaTime);

        externalVelocity = Vector3.MoveTowards(externalVelocity, externalTargetVelocity, externalAcceleration * Time.deltaTime);
        externalTargetVelocity = Vector3.MoveTowards(externalTargetVelocity, Vector3.zero, externalDeceleration * Time.deltaTime);

        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = groundedStickForce;

            if (jumpEnabled && Input.GetKeyDown(jumpKey))
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 finalVelocity = horizontalVelocity + externalVelocity;
        finalVelocity.y = verticalVelocity;

        controller.Move(finalVelocity * Time.deltaTime);
    }
}