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

        private NetworkVariable<float> _currentBatteryPercent = new NetworkVariable<float>(0f);
        private bool _isPlayerTurningOnFlashlight;
        
        private bool _isFlashlightOn;
        private bool _isBatteryDied;
        
        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _currentBatteryPercent.Value = batteryPercentMax;
            }

            _currentBatteryPercent.OnValueChanged += OnBatteryChanged;
            
            if (IsOwner)
            {
                inputReader.OnFlashlightEvent += InputReader_OnFlashlightEvent;
            }
            
        }
        
        private void OnBatteryChanged(float previous, float current)
        {
            OnBatteryPercentChangedEvent?.Invoke((int)current);
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
            if (!IsServer) return;
            if (!_isFlashlightOn) return;
            
            DecreaseFlashlightBattery();
            
        }
        
        private void DecreaseFlashlightBattery()
        {
            if (!_isBatteryDied && _isFlashlightOn)
            {
                _currentBatteryPercent.Value -= batteryPercentDecreasePerSecond * Time.deltaTime;
                
                if (_currentBatteryPercent.Value <= 0)
                {
                    _currentBatteryPercent.Value = 0;
                    _isBatteryDied = true;
                    TurnOffFlashlightRpc();
                }
            }
        }
        
        public void IncreaseFlashlightBattery(int batteryPercentIncrease)
        {
            if (!IsServer) return;

            _currentBatteryPercent.Value += batteryPercentIncrease;

            if (_currentBatteryPercent.Value > 0)
            {
                _isBatteryDied = false;
            }
            
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

