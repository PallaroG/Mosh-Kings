using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterController))]
public class MovimentPlayer : MonoBehaviour
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
    [SerializeField] private float externalAcceleration = 30f; // entra rápido no tranco
    [SerializeField] private float externalDeceleration = 20f; // desacelera ao longo do tempo
    [SerializeField] private float maxExternalSpeed = 10f;     // limite de velocidade externa acumulada

    [Header("Input")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    private CharacterController controller;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;

    // Empurrão / knockback
    private Vector3 externalVelocity;
    private Vector3 externalTargetVelocity;

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

    private void Update()
    {
        HandleMovement();
    }

    // Chamado por inimigos para aplicar empurrão no CharacterController
    public void AddPush(Vector3 direction, float force)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        direction.Normalize();
        externalTargetVelocity += direction * force;
        externalTargetVelocity = Vector3.ClampMagnitude(externalTargetVelocity, maxExternalSpeed);
    }

    private void HandleMovement()
    {
        // Input
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(x, z);
        input = Vector2.ClampMagnitude(input, 1f);

        // Direções relativas à câmera (sem componente Y)
        Vector3 forward = cameraRoot.forward;
        Vector3 right = cameraRoot.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 desiredMove = (forward * input.y + right * input.x);

        // Velocidade alvo
        bool isSprinting = Input.GetKey(sprintKey);
        float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        Vector3 targetVelocity = desiredMove * targetSpeed;

        // Aceleração orgânica
        float control = controller.isGrounded ? 1f : airControlPercent;
        float accelRate = (targetVelocity.sqrMagnitude > 0.01f) ? acceleration : deceleration;
        horizontalVelocity = Vector3.MoveTowards(
            horizontalVelocity,
            targetVelocity,
            accelRate * control * Time.deltaTime
        );

        // Empurrão: acelera em direção ao alvo e desacelera com o tempo
        externalVelocity = Vector3.MoveTowards(
            externalVelocity,
            externalTargetVelocity,
            externalAcceleration * Time.deltaTime
        );

        externalTargetVelocity = Vector3.MoveTowards(
            externalTargetVelocity,
            Vector3.zero,
            externalDeceleration * Time.deltaTime
        );

        // Pulo e gravidade
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f)
                verticalVelocity = groundedStickForce;

            if (jumpEnabled && Input.GetKeyDown(jumpKey))
            {
                // v = sqrt(h * -2g)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        verticalVelocity += gravity * Time.deltaTime;

        // Movimento final = input + empurrão
        Vector3 finalVelocity = horizontalVelocity + externalVelocity;
        finalVelocity.y = verticalVelocity;

        controller.Move(finalVelocity * Time.deltaTime);
    }
}