using Components;
using Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class StaminaBarUi : NetworkBehaviour
{
    [SerializeField] private PlayerRun playerRun;
    [SerializeField] private Image staminaFill;


    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            playerRun.OnStaminaChanged += PlayerRun_OnHealthChanged;

            float initial = playerRun.MaxStamina;
            UpdateBar(initial);
        }
    }
        
        
    private void PlayerRun_OnHealthChanged(float currentHealth)
    {
        UpdateBar(currentHealth);
    }

    private void UpdateBar(float current)
    {
        staminaFill.fillAmount = current / playerRun.MaxStamina;
    }


    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            playerRun.OnStaminaChanged -= PlayerRun_OnHealthChanged;
        }
    }
}
