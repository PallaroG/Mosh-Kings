using UnityEngine;

public class BillboardToggle : MonoBehaviour
{
    [Header("Configuração")]
    [SerializeField] private bool billboardAtivo = true;
    [SerializeField] private bool travarEixoY = false; // útil para sprites/placas no chão

    private Camera cam;

    private void Start()
    {
        cam = Camera.main;
    }

    private void LateUpdate()
    {
        if (!billboardAtivo) return;
        if (cam == null) return;

        if (travarEixoY)
        {
            // Olha para a câmera sem inclinar pra cima/baixo
            Vector3 direcao = cam.transform.position - transform.position;
            direcao.y = 0f;

            if (direcao.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(-direcao);
        }
        else
        {
            // Billboarding completo
            transform.LookAt(transform.position + cam.transform.rotation * Vector3.forward,
                             cam.transform.rotation * Vector3.up);
        }
    }

    // Métodos públicos para ativar/desativar via código, botão, trigger etc.
    public void AtivarBillboard()
    {
        billboardAtivo = true;
    }

    public void DesativarBillboard()
    {
        billboardAtivo = false;
    }

    public void AlternarBillboard()
    {
        billboardAtivo = !billboardAtivo;
    }
}