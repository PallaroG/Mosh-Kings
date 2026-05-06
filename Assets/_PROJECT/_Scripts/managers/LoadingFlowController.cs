using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum LoadingFlowStep
{
    Idle,
    StartingRelay,
    WaitingForConnection,
    LoadingLobby,
    WaitingForSceneSync,
    WaitingForLocalPlayer,
    Ready,
    Failed
}

public class LoadingFlowController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RelayBootstrap relayBootstrap;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Slider progressSlider;

    [Header("Scenes")]
    [SerializeField] private string lobbySceneName = "lobby";

    [Header("Timing")]
    [SerializeField] private float connectionTimeoutSeconds = 30f;
    [SerializeField] private float sceneTimeoutSeconds = 30f;
    [SerializeField] private float playerTimeoutSeconds = 30f;
    [SerializeField] private bool persistUntilPlayerReady = true;
    [SerializeField] private bool destroyWhenReady = true;

    [Header("Events")]
    public UnityEvent<string> OnStatusChanged;
    public UnityEvent<float> OnProgressChanged;
    public UnityEvent<LoadingFlowStep> OnStepChanged;

    public LoadingFlowStep CurrentStep { get; private set; } = LoadingFlowStep.Idle;
    public string CurrentStatus { get; private set; } = string.Empty;
    public float CurrentProgress { get; private set; }

    private bool flowStarted;

    private async void Awake()
    {
        if (persistUntilPlayerReady)
            DontDestroyOnLoad(gameObject);

        await Task.Yield();
    }

    private async void Start()
    {
        if (flowStarted) return;
        flowStarted = true;

        await RunLoadingFlowAsync();
    }

    public async Task RunLoadingFlowAsync()
    {
        var state = MultiplayerConnectionState.Instance;
        state.ClearError();

        if (relayBootstrap == null)
            relayBootstrap = FindAnyObjectByType<RelayBootstrap>();

        if (relayBootstrap == null)
        {
            Fail("RelayBootstrap not found in Loading scene.");
            return;
        }

        if (state.PendingMode == PendingConnectionMode.None)
        {
            Fail("No pending multiplayer connection mode.");
            return;
        }

        bool started = false;

        try
        {
            SetStep(LoadingFlowStep.StartingRelay, "Starting multiplayer session...", 0.1f);

            if (state.PendingMode == PendingConnectionMode.Host)
            {
                started = await relayBootstrap.StartHostWithRelayAsync(loadGameSceneAfterStart: false);
                state.SetGeneratedJoinCode(relayBootstrap.LastJoinCode);
            }
            else if (state.PendingMode == PendingConnectionMode.Client)
            {
                if (string.IsNullOrWhiteSpace(state.PendingJoinCode))
                {
                    Fail("Join Code is empty.");
                    return;
                }

                started = await relayBootstrap.StartClientWithRelayAsync(state.PendingJoinCode);
                state.SetGeneratedJoinCode(relayBootstrap.LastJoinCode);
            }
        }
        catch (Exception e)
        {
            Fail(e.Message);
            return;
        }

        if (!started)
        {
            Fail("Network session could not start.");
            return;
        }

        if (!await WaitForConnectionAsync())
            return;

        if (state.PendingMode == PendingConnectionMode.Host)
        {
            if (!LoadLobbyAsHost())
                return;
        }
        else
        {
            SetStep(LoadingFlowStep.WaitingForSceneSync, "Waiting for host scene sync...", 0.55f);
        }

        if (!await WaitForLobbySceneAsync())
            return;

        if (!await WaitForLocalPlayerAsync())
            return;

        state.ClearPending();
        SetStep(LoadingFlowStep.Ready, "Ready.", 1f);

        if (destroyWhenReady)
            Destroy(gameObject);
    }

    private async Task<bool> WaitForConnectionAsync()
    {
        SetStep(LoadingFlowStep.WaitingForConnection, "Waiting for network connection...", 0.3f);

        float startTime = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startTime < connectionTimeoutSeconds)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening && networkManager.IsConnectedClient)
                return true;

            await Task.Yield();
        }

        Fail("Timed out waiting for network connection.");
        return false;
    }

    private bool LoadLobbyAsHost()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsServer)
        {
            Fail("Only the server can load the Lobby scene.");
            return false;
        }

        SetStep(LoadingFlowStep.LoadingLobby, "Loading Lobby...", 0.45f);
        var result = networkManager.SceneManager.LoadScene(GetLobbySceneName(), LoadSceneMode.Single);

        if (result == SceneEventProgressStatus.Started)
            return true;

        Fail($"Could not start Lobby scene load. Status: {result}");
        return false;
    }

    private async Task<bool> WaitForLobbySceneAsync()
    {
        SetStep(LoadingFlowStep.WaitingForSceneSync, "Waiting for Lobby scene...", 0.65f);

        string targetScene = GetLobbySceneName();
        float startTime = Time.realtimeSinceStartup;

        while (Time.realtimeSinceStartup - startTime < sceneTimeoutSeconds)
        {
            if (SceneManager.GetActiveScene().name == targetScene)
                return true;

            await Task.Yield();
        }

        Fail($"Timed out waiting for scene '{targetScene}'.");
        return false;
    }

    private async Task<bool> WaitForLocalPlayerAsync()
    {
        SetStep(LoadingFlowStep.WaitingForLocalPlayer, "Waiting for local player...", 0.85f);

        float startTime = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startTime < playerTimeoutSeconds)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            NetworkObject playerObject = networkManager != null && networkManager.LocalClient != null
                ? networkManager.LocalClient.PlayerObject
                : null;

            if (playerObject != null && playerObject.IsSpawned)
                return true;

            await Task.Yield();
        }

        Fail("Timed out waiting for local player spawn.");
        return false;
    }

    private string GetLobbySceneName()
    {
        if (!string.IsNullOrWhiteSpace(lobbySceneName))
            return lobbySceneName;

        return relayBootstrap != null ? relayBootstrap.GameSceneName : "lobby";
    }

    private void SetStep(LoadingFlowStep step, string status, float progress)
    {
        CurrentStep = step;
        CurrentStatus = status;
        CurrentProgress = Mathf.Clamp01(progress);

        if (statusText != null)
            statusText.text = status;

        if (progressSlider != null)
            progressSlider.value = CurrentProgress;

        OnStepChanged?.Invoke(CurrentStep);
        OnStatusChanged?.Invoke(CurrentStatus);
        OnProgressChanged?.Invoke(CurrentProgress);

        Debug.Log($"[LoadingFlow] {CurrentStep}: {CurrentStatus}");
    }

    private void Fail(string error)
    {
        MultiplayerConnectionState.Instance.SetError(error);
        SetStep(LoadingFlowStep.Failed, error, CurrentProgress);
        Debug.LogError($"[LoadingFlow] {error}");
    }
}
