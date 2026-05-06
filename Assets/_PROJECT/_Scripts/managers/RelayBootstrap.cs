using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using UnityEngine.SceneManagement;

public class RelayBootstrap : MonoBehaviour
{
    // Singleton para acessarmos facilmente de outras cenas
    public static RelayBootstrap Instance { get; private set; }

    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport unityTransport;
    [SerializeField] private int maxConnections = 8;
    [SerializeField] private string nomeDaCenaDoJogo = "lobby"; // Nome da sua cena

    public string GameSceneName => nomeDaCenaDoJogo;
    public NetworkManager CurrentNetworkManager
    {
        get
        {
            CacheReferences();
            return networkManager;
        }
    }

    public string LastJoinCode { get; private set; }

    private async void Awake()
    {
        // Configuração do Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CacheReferences();
        
        DontDestroyOnLoad(gameObject);
        if (networkManager != null) DontDestroyOnLoad(networkManager.gameObject);
        
        await EnsureInitialized();
    }

    public void HostWithRelay()
    {
        _ = StartHostWithRelayAsync(loadGameSceneAfterStart: true);
    }

    public async Task<bool> StartHostWithRelayAsync(bool loadGameSceneAfterStart)
    {
        try
        {
            CacheReferences();
            await EnsureInitialized();

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            LastJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var relayServerData = allocation.ToRelayServerData("dtls");
            unityTransport.SetRelayServerData(relayServerData);

            if (networkManager.StartHost())
            {
                Debug.Log($"[Relay] Host iniciado. JoinCode: {LastJoinCode}");
                GUIUtility.systemCopyBuffer = LastJoinCode; 

                MultiplayerConnectionState.Instance.SetGeneratedJoinCode(LastJoinCode);

                if (loadGameSceneAfterStart)
                {
                    // AGUARDA MEIO SEGUNDO para o Netcode estabilizar antes de mudar de cena
                    await Task.Delay(500);
                    networkManager.SceneManager.LoadScene(nomeDaCenaDoJogo, LoadSceneMode.Single);
                }

                return true;
            }

            Debug.Log("[Relay] Falha ao iniciar host.");
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] HostWithRelay erro: {e}");
            MultiplayerConnectionState.Instance.SetError(e.Message);
            return false;
        }
    }

    public void JoinWithRelay(string joinCode)
    {
        _ = StartClientWithRelayAsync(joinCode);
    }

    public async Task<bool> StartClientWithRelayAsync(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            MultiplayerConnectionState.Instance.SetError("Join Code is empty.");
            return false;
        }

        try
        {
            CacheReferences();
            await EnsureInitialized();
            joinCode = joinCode.Trim().ToUpperInvariant();

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            var relayServerData = joinAllocation.ToRelayServerData("dtls");
            unityTransport.SetRelayServerData(relayServerData);

            // Salva o código localmente para o client poder visualizar também
            LastJoinCode = joinCode;

            if (networkManager.StartClient())
            {
                Debug.Log($"[Relay] Client conectado com code: {joinCode}");
                return true;
            }

            Debug.Log("[Relay] Falha ao iniciar client.");
            return false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] JoinWithRelay erro: {e}");
            MultiplayerConnectionState.Instance.SetError(e.Message);
            return false;
        }
    }

    private void CacheReferences()
    {
        if (networkManager == null) networkManager = FindAnyObjectByType<NetworkManager>();
        if (unityTransport == null) unityTransport = FindAnyObjectByType<UnityTransport>();
    }

    private static async Task EnsureInitialized()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
}
