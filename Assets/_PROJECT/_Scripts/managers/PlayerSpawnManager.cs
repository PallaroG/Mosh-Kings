using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerSpawnManager : MonoBehaviour
{
    [SerializeField] private List<Transform> spawnPoints = new();
    private int nextSpawnIndex = 0;

    private void Awake()
    {
        if (spawnPoints.Count == 0)
        {
            var tagged = GameObject.FindGameObjectsWithTag("SpawnPoint");
            foreach (var go in tagged) spawnPoints.Add(go.transform);
        }
    }

    private void OnEnable()
    {
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        var client = NetworkManager.Singleton.ConnectedClients[clientId];
        if (client.PlayerObject == null) return;

        Transform spawn = GetNextSpawnPoint();
        if (spawn == null) return;

        // Teleporta player para o spawn correto
        client.PlayerObject.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
    }

    private Transform GetNextSpawnPoint()
    {
        if (spawnPoints.Count == 0) return null;
        Transform p = spawnPoints[nextSpawnIndex % spawnPoints.Count];
        nextSpawnIndex++;
        return p;
    }
}