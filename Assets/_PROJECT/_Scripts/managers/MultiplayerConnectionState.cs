using UnityEngine;

public enum PendingConnectionMode
{
    None,
    Host,
    Client
}

public class MultiplayerConnectionState : MonoBehaviour
{
    private static MultiplayerConnectionState instance;

    public static MultiplayerConnectionState Instance
    {
        get
        {
            if (instance != null) return instance;

            var existing = FindAnyObjectByType<MultiplayerConnectionState>();
            if (existing != null)
            {
                instance = existing;
                return instance;
            }

            var go = new GameObject(nameof(MultiplayerConnectionState));
            instance = go.AddComponent<MultiplayerConnectionState>();
            DontDestroyOnLoad(go);
            return instance;
        }
    }

    public PendingConnectionMode PendingMode { get; private set; } = PendingConnectionMode.None;
    public string PendingJoinCode { get; private set; } = string.Empty;
    public string GeneratedJoinCode { get; private set; } = string.Empty;
    public string CurrentError { get; private set; } = string.Empty;

    public bool HasError => !string.IsNullOrWhiteSpace(CurrentError);

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void PrepareHost()
    {
        PendingMode = PendingConnectionMode.Host;
        PendingJoinCode = string.Empty;
        GeneratedJoinCode = string.Empty;
        CurrentError = string.Empty;
    }

    public void PrepareClient(string joinCode)
    {
        PendingMode = PendingConnectionMode.Client;
        PendingJoinCode = NormalizeJoinCode(joinCode);
        GeneratedJoinCode = string.Empty;
        CurrentError = string.Empty;
    }

    public void SetGeneratedJoinCode(string joinCode)
    {
        GeneratedJoinCode = NormalizeJoinCode(joinCode);
    }

    public void SetError(string error)
    {
        CurrentError = string.IsNullOrWhiteSpace(error) ? "Unknown connection error." : error;
    }

    public void ClearError()
    {
        CurrentError = string.Empty;
    }

    public void ClearPending()
    {
        PendingMode = PendingConnectionMode.None;
        PendingJoinCode = string.Empty;
    }

    public void ResetState()
    {
        PendingMode = PendingConnectionMode.None;
        PendingJoinCode = string.Empty;
        GeneratedJoinCode = string.Empty;
        CurrentError = string.Empty;
    }

    private static string NormalizeJoinCode(string joinCode)
    {
        return string.IsNullOrWhiteSpace(joinCode) ? string.Empty : joinCode.Trim().ToUpperInvariant();
    }
}
