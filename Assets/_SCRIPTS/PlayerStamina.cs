using UnityEngine;
using System;

public class PlayerStamina : MonoBehaviour
{
    [Header("Cansaço")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina;

    // UI escuta esse evento
    public event Action<float, float> OnStaminaChanged;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;

    private void Awake()
    {
        currentStamina = maxStamina;
        NotifyChange();
    }

    public void ReceivePushDamage(float damage)
    {
        if (damage <= 0f) return;

        currentStamina = Mathf.Clamp(currentStamina - damage, 0f, maxStamina);
        NotifyChange();

        if (currentStamina <= 0f)
        {
            Debug.Log("Exausto!");
        }
    }

    public void Recover(float amount)
    {
        if (amount <= 0f) return;

        currentStamina = Mathf.Clamp(currentStamina + amount, 0f, maxStamina);
        NotifyChange();
    }

    private void NotifyChange()
    {
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }
}