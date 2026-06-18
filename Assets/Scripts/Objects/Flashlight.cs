using System;
using Inputs;
using Player;
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
        [SerializeField] private PlayerState playerState;

        [SerializeField] private int batteryPercentMax = 100;
        [SerializeField] private int batteryPercentDecreasePerSecond = 10;

        public int BatteryPercentMax => batteryPercentMax;
        
        private NetworkVariable<float> _currentBattery = new NetworkVariable<float>();
        private NetworkVariable<bool> _isFlashlightOn = new NetworkVariable<bool>();

        private bool _batteryDead;
        private bool _isPlayerDead;
        private bool _isPlayerLocked;

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
                playerState.OnPlayerDead += PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked += PlayerState_OnPlayerLocked;
            }

            UpdateFlashlightVisual(_isFlashlightOn.Value);
        }
        
        private void PlayerState_OnPlayerDead(bool isPlayerDead) => _isPlayerDead = isPlayerDead;
        private void PlayerState_OnPlayerLocked(bool isPlayerLocked) => _isPlayerLocked = isPlayerLocked;
        
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
            if(_isPlayerLocked || _isPlayerDead) return;
            
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
            Debug.Log($"IncreaseFlashlightBattery  {_currentBattery.Value}");
            
            if (_currentBattery.Value > batteryPercentMax)
            {
                _currentBattery.Value = batteryPercentMax;
                Debug.Log($"Cap BatteryValue:  {_currentBattery.Value}");
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
                playerState.OnPlayerDead -= PlayerState_OnPlayerDead;
                playerState.OnPlayerLocked -= PlayerState_OnPlayerLocked;
            }
            
        }
        
    }
}