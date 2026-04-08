using UnityEngine;
using UnityEngine.UI;

public class StaminaBarUI : MonoBehaviour
{
    [SerializeField] private PlayerStamina playerStamina;
    [SerializeField] private Slider slider;

    private void Awake()
    {
        if (playerStamina == null)
            playerStamina = FindObjectOfType<PlayerStamina>();

        if (slider == null)
            slider = GetComponent<Slider>();
    }

    private void OnEnable()
    {
        if (playerStamina != null)
            playerStamina.OnStaminaChanged += UpdateBar;
    }

    private void Start()
    {
        if (playerStamina != null)
            UpdateBar(playerStamina.CurrentStamina, playerStamina.MaxStamina);
    }

    private void OnDisable()
    {
        if (playerStamina != null)
            playerStamina.OnStaminaChanged -= UpdateBar;
    }

    private void UpdateBar(float current, float max)
    {
        slider.value = (max <= 0f) ? 0f : current / max;
    }
}