using Objects;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class FlashlightBarUi : NetworkBehaviour
{
    [SerializeField] private Flashlight playerFlashlight;
    [SerializeField] private Image batteryFill;


    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            playerFlashlight.OnBatteryPercentChangedEvent += PlayerFlashlight_OnBatteryPercentChanged;

            int initial = playerFlashlight.BatteryPercentMax;
            UpdateBar(initial);
        }
    }
        
        
    private void PlayerFlashlight_OnBatteryPercentChanged(int currentBattery)
    {
        UpdateBar(currentBattery);
    }

    private void UpdateBar(int current)
    {
        batteryFill.fillAmount = current / (float)playerFlashlight.BatteryPercentMax;
    }


    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            playerFlashlight.OnBatteryPercentChangedEvent -= PlayerFlashlight_OnBatteryPercentChanged;
        }
    }
}
