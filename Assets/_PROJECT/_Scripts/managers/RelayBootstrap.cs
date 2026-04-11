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

public class RelayBootstrap : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport unityTransport;
    [SerializeField] private int maxConnections = 8;

    public string LastJoinCode { get; private set; }

    private async void Awake()
    {
        if (networkManager == null) networkManager = FindAnyObjectByType<NetworkManager>();
        if (unityTransport == null) unityTransport = FindAnyObjectByType<UnityTransport>();
        await EnsureInitialized();
    }

    public async void HostWithRelay()
    {
        try
        {
            await EnsureInitialized();

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            LastJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // A correção real: usar o método de extensão ToRelayServerData, sem instanciar a classe
            var relayServerData = allocation.ToRelayServerData("dtls");
            unityTransport.SetRelayServerData(relayServerData);

            Debug.Log(networkManager.StartHost()
                ? $"[Relay] Host iniciado. JoinCode: {LastJoinCode}"
                : "[Relay] Falha ao iniciar host.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] HostWithRelay erro: {e}");
        }
    }

    public async void JoinWithRelay(string joinCode)
    {
        // 1. Trava de segurança: impede de rodar se o texto for nulo ou vazio
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            Debug.LogWarning("[Relay] Operação cancelada: Você tentou entrar, mas o código da sala está vazio!");
            return; 
        }

        try
        {
            await EnsureInitialized();

            // Formata o código para garantir que não tenha espaços acidentais no começo ou fim
            joinCode = joinCode.Trim().ToUpperInvariant();

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var relayServerData = joinAllocation.ToRelayServerData("dtls");
            unityTransport.SetRelayServerData(relayServerData);

            Debug.Log(networkManager.StartClient()
                ? $"[Relay] Client conectado com code: {joinCode}"
                : "[Relay] Falha ao iniciar client.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] JoinWithRelay erro: {e}");
        }
    }

    private static async Task EnsureInitialized()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }
}