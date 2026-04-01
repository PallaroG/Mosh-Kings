using UnityEngine;

public class CameraScript : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Transform playerBody;

    [Header("Sensibilidade")]
    [SerializeField] private float mouseSensitivity = 180f;
    [SerializeField] private bool invertY = false;

    [Header("Limites Verticais")]
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Suavização")]
    [SerializeField] private bool smoothLook = true;
    [SerializeField] private float smoothTime = 0.03f;

    private float pitch;
    private Vector2 currentMouseDelta;
    private Vector2 currentMouseDeltaVelocity;

    private void Start()
    {
        if (playerBody == null)
            Debug.LogWarning("FPSCameraLook: playerBody não definido.");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
    }

    private void HandleLook()
    {
        Vector2 targetMouseDelta = new Vector2(
            Input.GetAxisRaw("Mouse X"),
            Input.GetAxisRaw("Mouse Y")
        ) * mouseSensitivity * Time.deltaTime;

        if (smoothLook)
        {
            currentMouseDelta = Vector2.SmoothDamp(
                currentMouseDelta,
                targetMouseDelta,
                ref currentMouseDeltaVelocity,
                smoothTime
            );
        }
        else
        {
            currentMouseDelta = targetMouseDelta;
        }

        // Rotação horizontal no corpo
        if (playerBody != null)
            playerBody.Rotate(Vector3.up * currentMouseDelta.x);

        // Rotação vertical na câmera
        float yDelta = invertY ? currentMouseDelta.y : -currentMouseDelta.y;
        pitch = Mathf.Clamp(pitch + yDelta, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
