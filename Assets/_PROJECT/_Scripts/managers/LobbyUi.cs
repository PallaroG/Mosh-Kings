using UnityEngine;
using TMPro; // Para manipular TextMeshPro

public class LobbyUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TMP_Text codeTextDisplay;

    private void Start()
    {
        // Assim que a cena abre, ele tenta buscar o código no RelayBootstrap
        if (RelayBootstrap.Instance != null && !string.IsNullOrEmpty(RelayBootstrap.Instance.LastJoinCode))
        {
            codeTextDisplay.text = $"CÓDIGO: {RelayBootstrap.Instance.LastJoinCode}";
        }
        else
        {
            codeTextDisplay.text = "CÓDIGO: (Não conectado via Relay)";
        }
    }

    // Botão opcional para copiar o código estando dentro do lobby
    public void OnClickCopyCode()
    {
        if (RelayBootstrap.Instance != null && !string.IsNullOrEmpty(RelayBootstrap.Instance.LastJoinCode))
        {
            GUIUtility.systemCopyBuffer = RelayBootstrap.Instance.LastJoinCode;
            Debug.Log("Código copiado no Lobby!");
        }
    }
}