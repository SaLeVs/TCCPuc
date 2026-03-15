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

        private NetworkVariable<float> _currentBattery = new NetworkVariable<float>();
        private NetworkVariable<bool> _isFlashlightOn = new NetworkVariable<bool>();

        private bool _batteryDead;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _currentBattery.Value = batteryPercentMax;
            }

            _currentBattery.OnValueChanged += OnBatteryChanged;
            _isFlashlightOn.OnValueChanged += OnFlashlightStateChanged;

            if (IsOwner)
            {
                inputReader.OnFlashlightEvent += OnFlashlightInput;
            }

            UpdateFlashlightVisual(_isFlashlightOn.Value);
        }

        private void OnBatteryChanged(float previous, float current)
        {
            OnBatteryPercentChangedEvent?.Invoke((int)current);
        }
        
        private void OnFlashlightStateChanged(bool previous, bool current)
        {
            UpdateFlashlightVisual(current);
        }
        
        private void OnFlashlightInput() 
        {
            ToggleFlashlightServerRpc();
        }

        
        [Rpc(SendTo.Server)]
        private void ToggleFlashlightServerRpc()
        {
            if (_batteryDead) return;

            _isFlashlightOn.Value = !_isFlashlightOn.Value;
        }
        
        private void UpdateFlashlightVisual(bool state)
        {
            flashlight.enabled = state;
            lightBeam.SetActive(state);
        }
        
        private void Update()
        {
            if (!IsServer) return;
            if (!_isFlashlightOn.Value) return;

            DecreaseFlashlightBattery();
            
        }

        private void DecreaseFlashlightBattery()
        {
            _currentBattery.Value -= batteryPercentDecreasePerSecond * Time.deltaTime;

            if (_currentBattery.Value <= 0)
            {
                _currentBattery.Value = 0;
                _batteryDead = true;
                _isFlashlightOn.Value = false;
            }
            
        }
        
        public void IncreaseFlashlightBattery(int amount)
        {
            if (!IsServer) return;

            _currentBattery.Value += amount;

            if (_currentBattery.Value > batteryPercentMax)
            {
                _currentBattery.Value = batteryPercentMax;
            }

            if (_currentBattery.Value > 0)
            {
                _batteryDead = false;
            }
            
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnFlashlightEvent -= OnFlashlightInput;
            }
            
        }
        
    }
}