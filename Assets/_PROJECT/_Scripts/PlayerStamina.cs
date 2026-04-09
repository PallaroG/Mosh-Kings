using UnityEngine;
using System;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
public class PlayerStamina : NetworkBehaviour
{
    [Header("Cansaço")]
    [SerializeField] private float maxStamina = 100f;

    private NetworkVariable<float> currentStamina = new(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public event Action<float, float> OnStaminaChanged;

    public float CurrentStamina => currentStamina.Value;
    public float MaxStamina => maxStamina;

    public override void OnNetworkSpawn()
    {
        currentStamina.OnValueChanged += OnStaminaValueChanged;

        if (IsServer)
            currentStamina.Value = maxStamina;

        OnStaminaChanged?.Invoke(currentStamina.Value, maxStamina);
    }

    public override void OnNetworkDespawn()
    {
        currentStamina.OnValueChanged -= OnStaminaValueChanged;
    }

    private void OnStaminaValueChanged(float oldValue, float newValue)
    {
        OnStaminaChanged?.Invoke(newValue, maxStamina);

        if (newValue <= 0f && oldValue > 0f)
            Debug.Log("Exausto!");
    }

    public void ReceivePushDamage(float damage)
    {
        if (damage <= 0f) return;

        if (IsServer) ApplyDamageServer(damage);
        else ReceivePushDamageServerRpc(damage);
    }

    public void Recover(float amount)
    {
        if (amount <= 0f) return;

        if (IsServer) RecoverServer(amount);
        else RecoverServerRpc(amount);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ReceivePushDamageServerRpc(float damage)
    {
        ApplyDamageServer(damage);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RecoverServerRpc(float amount)
    {
        RecoverServer(amount);
    }

    private void ApplyDamageServer(float damage)
    {
        currentStamina.Value = Mathf.Clamp(currentStamina.Value - damage, 0f, maxStamina);
    }

    private void RecoverServer(float amount)
    {
        currentStamina.Value = Mathf.Clamp(currentStamina.Value + amount, 0f, maxStamina);
    }
}