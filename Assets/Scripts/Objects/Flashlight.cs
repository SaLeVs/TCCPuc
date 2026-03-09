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
        
        [SerializeField] private int batteryPercentMax = 100;
        [SerializeField] private int batteryPercentDecreasePerSecond = 10;
        
        private float _currentBatteryPercent;
        private bool _isPlayerTurningOnFlashlight;
        
        private bool _isFlashlightOn;
        private bool _isBatteryDied;
        
        
        public override void OnNetworkSpawn()
        {
            _currentBatteryPercent = batteryPercentMax;
            
            Debug.Log(_currentBatteryPercent);
            
            if (IsOwner)
            {
                inputReader.OnFlashlightEvent += InputReader_OnFlashlightEvent;
            }
            
        }
        
        private void InputReader_OnFlashlightEvent(bool isPlayerTurningOnFlashlight)
        {
            if (isPlayerTurningOnFlashlight && !_isFlashlightOn && !_isBatteryDied)
            {
                TurnOnFlashlight();
            }
            else if(!isPlayerTurningOnFlashlight && _isFlashlightOn)
            {
                TurnOffFlashlight();
            }
        }
       
        private void TurnOnFlashlight()
        {
            flashlight.enabled = true;
            _isFlashlightOn = true;
        }
        
        private void TurnOffFlashlight()
        {
            flashlight.enabled = false;
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
                Debug.Log(_currentBatteryPercent);
                
                if (_currentBatteryPercent <= 0)
                {
                    _currentBatteryPercent = 0;
                    _isBatteryDied = true;
                    TurnOffFlashlight();
                }
            }
        }
        
        private void IncreaseFlashlightBattery(int batteryPercentIncrease)
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

