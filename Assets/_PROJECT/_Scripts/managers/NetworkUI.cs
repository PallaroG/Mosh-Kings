using UnityEngine;
using Unity.Netcode;

public class NetworkUI : MonoBehaviour
{
    // Criamos nossas próprias funções "void" para a Unity conseguir enxergar
    public void LigarHost()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void LigarClient()
    {
        NetworkManager.Singleton.StartClient();
    }

    public void LigarServer()
    {
        NetworkManager.Singleton.StartServer();
    }
}
