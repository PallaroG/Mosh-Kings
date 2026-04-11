using UnityEngine;
using TMPro; // Necessário para acessar os componentes TextMeshPro

public class RelayMenuUI : MonoBehaviour
{
    [Header("Script Principal")]
    [SerializeField] private RelayBootstrap relayBootstrap;

    [Header("Elementos da UI")]
    [SerializeField] private TMP_InputField joinCodeInput; // Arraste seu InputField aqui
    [SerializeField] private TMP_Text joinCodeOutput;      // Onde o código vai aparecer para o Host copiar
    [SerializeField] private TMP_Text statusText;          // (Opcional) Texto para mostrar "Conectando..."

    private void Awake()
    {
        // Puxa automaticamente se você esquecer de arrastar no Inspector
        if (relayBootstrap == null)
            relayBootstrap = FindAnyObjectByType<RelayBootstrap>();
    }

    // 1. LIGUE ESTA FUNÇÃO NO BOTÃO "HOST"
    public void OnClickHost()
    {
        if (relayBootstrap == null) return;

        SetStatus("Criando sala Relay...");
        relayBootstrap.HostWithRelay();
        
        // Dá um pequeno delay para a rede gerar o código e então exibe na tela
        Invoke(nameof(RefreshHostCode), 1.0f); 
    }

    // 2. LIGUE ESTA FUNÇÃO NO BOTÃO "JOIN"
    public void OnClickJoin()
    {
        if (relayBootstrap == null || joinCodeInput == null) return;

        // Pega exatamente o texto que o jogador digitou no InputField
        string typedCode = joinCodeInput.text.Trim().ToUpper();

        if (string.IsNullOrWhiteSpace(typedCode))
        {
            SetStatus("Erro: Digite o código da sala primeiro!");
            return;
        }

        SetStatus($"Entrando na sala {typedCode}...");
        
        // Manda o texto digitado pro script do servidor
        relayBootstrap.JoinWithRelay(typedCode);
    }

    // (Opcional) Ligue num botão "Copiar Código"
    public void OnClickCopyCode()
    {
        if (relayBootstrap == null || string.IsNullOrWhiteSpace(relayBootstrap.LastJoinCode)) return;

        GUIUtility.systemCopyBuffer = relayBootstrap.LastJoinCode;
        SetStatus("Código copiado para a área de transferência!");
    }

    private void RefreshHostCode()
    {
        if (relayBootstrap == null) return;

        string code = relayBootstrap.LastJoinCode;
        if (!string.IsNullOrWhiteSpace(code))
        {
            if (joinCodeOutput != null) joinCodeOutput.text = $"CÓDIGO: {code}";
            SetStatus("Sala criada! Passe o código para seu amigo.");
        }
        else
        {
            // Se demorou pra gerar, tenta checar de novo em meio segundo
            Invoke(nameof(RefreshHostCode), 0.5f);
        }
    }

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log($"[Relay UI] {msg}");
    }
}