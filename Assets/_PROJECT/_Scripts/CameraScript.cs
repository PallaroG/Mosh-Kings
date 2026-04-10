using UnityEngine;
using Unity.Netcode;

public class CameraScript : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Transform playerBody;
    [SerializeField] private Camera localCamera;
    [SerializeField] private AudioListener audioListenerRef;
    [SerializeField] private NetworkObject playerNetworkObject; // root do player

    [Header("Sensibilidade")]
    [SerializeField] private float mouseSensitivity = 180f;
    [SerializeField] private bool invertY = false;

    [Header("Limites Verticais")]
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Suavização")]
    [SerializeField] private bool smoothLook = true;
    [SerializeField] private float smoothTime = 0.03f;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnStart = true;
    [SerializeField] private bool unlockWithAlt = true;
    [SerializeField] private KeyCode altKey = KeyCode.LeftAlt;

    private float pitch;
    private Vector2 currentMouseDelta;
    private Vector2 currentMouseDeltaVelocity;
    private bool initialized;
    private bool isLocalOwner;

    private bool IsAltHeld => Input.GetKey(altKey) || Input.GetKey(KeyCode.RightAlt);

    private void Awake()
    {
        if (localCamera == null) localCamera = GetComponentInChildren<Camera>(true);
        if (audioListenerRef == null) audioListenerRef = GetComponentInChildren<AudioListener>(true);
        if (playerNetworkObject == null) playerNetworkObject = GetComponentInParent<NetworkObject>();
        if (playerBody == null && playerNetworkObject != null) playerBody = playerNetworkObject.transform;
    }

    private void Start()
    {
        InitializeOwnerState();
    }

    private void InitializeOwnerState()
    {
        if (playerNetworkObject == null || !playerNetworkObject.IsSpawned)
        {
            Invoke(nameof(InitializeOwnerState), 0.1f); // espera spawn de rede
            return;
        }

        isLocalOwner = playerNetworkObject.IsOwner;

        if (localCamera != null)
        {
            localCamera.enabled = isLocalOwner;
            localCamera.tag = isLocalOwner ? "MainCamera" : "Untagged";
        }

        if (audioListenerRef != null)
            audioListenerRef.enabled = isLocalOwner;

        if (isLocalOwner && lockCursorOnStart)
            SetCursorLocked(true);

        initialized = true;
    }

    private void Update()
    {
        if (!initialized || !isLocalOwner) return;

        bool freeCursor = unlockWithAlt && IsAltHeld;
        SetCursorLocked(!freeCursor);

        if (!freeCursor)
            HandleLook();
    }

    private void OnDisable()
    {
        if (isLocalOwner) SetCursorLocked(false);
    }

    private void HandleLook()
    {
        Vector2 targetMouseDelta = new Vector2(
            Input.GetAxisRaw("Mouse X"),
            Input.GetAxisRaw("Mouse Y")
        ) * mouseSensitivity * Time.deltaTime;

        currentMouseDelta = smoothLook
            ? Vector2.SmoothDamp(currentMouseDelta, targetMouseDelta, ref currentMouseDeltaVelocity, smoothTime)
            : targetMouseDelta;

        if (playerBody != null)
            playerBody.Rotate(Vector3.up * currentMouseDelta.x);

        float yDelta = invertY ? currentMouseDelta.y : -currentMouseDelta.y;
        pitch = Mathf.Clamp(pitch + yDelta, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}