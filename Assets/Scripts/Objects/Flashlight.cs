using System;
using Inputs;
using Unity.Netcode;
using UnityEngine;

namespace Objects
{
    public class Flashlight : NetworkBehaviour
    {
        public event Action<int> OnBatteryPercentChangedEvent;
        
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Light flashlight;
        [SerializeField] private GameObject lightBeam;
        
        [SerializeField] private int batteryPercentMax = 100;
        [SerializeField] private int batteryPercentDecreasePerSecond = 10;
        
        private float _currentBatteryPercent;
        private bool _isPlayerTurningOnFlashlight;
        
        private bool _isFlashlightOn;
        private bool _isBatteryDied;
        
        
        public override void OnNetworkSpawn()
        {
            _currentBatteryPercent = batteryPercentMax;
            
            if (IsOwner)
            {
                inputReader.OnFlashlightEvent += InputReader_OnFlashlightEvent;
            }
            
        }
        
        private void InputReader_OnFlashlightEvent(bool isPlayerTurningOnFlashlight)
        {
            if (isPlayerTurningOnFlashlight && !_isFlashlightOn && !_isBatteryDied)
            {
                TurnOnFlashlightRpc();
            }
            else if(!isPlayerTurningOnFlashlight && _isFlashlightOn)
            {
                TurnOffFlashlightRpc();
            }
        }
       
        [Rpc(SendTo.ClientsAndHost)]
        private void TurnOnFlashlightRpc()
        {
            flashlight.enabled = true;
            lightBeam.SetActive(true);
            _isFlashlightOn = true;
        }
        
        [Rpc(SendTo.ClientsAndHost)]
        private void TurnOffFlashlightRpc()
        {
            flashlight.enabled = false;
            lightBeam.SetActive(false);
            _isFlashlightOn = false;
        }

        private void Update()
        {
            if (IsOwner)
            {
                if (!_isFlashlightOn) return;
                
                DecreaseFlashlightBattery();
            }
        }
        
        private void DecreaseFlashlightBattery()
        {
            if (!_isBatteryDied && _isFlashlightOn)
            {
                _currentBatteryPercent -= batteryPercentDecreasePerSecond * Time.deltaTime;
                OnBatteryPercentChangedEvent?.Invoke((int)_currentBatteryPercent);
                
                if (_currentBatteryPercent <= 0)
                {
                    _currentBatteryPercent = 0;
                    _isBatteryDied = true;
                    TurnOffFlashlightRpc();
                }
            }
        }
        
        public void IncreaseFlashlightBattery(int batteryPercentIncrease)
        {
            _currentBatteryPercent += batteryPercentIncrease;
            OnBatteryPercentChangedEvent?.Invoke((int)_currentBatteryPercent);
            
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnFlashlightEvent -= InputReader_OnFlashlightEvent;
            }
            
        }
        
    }
}

