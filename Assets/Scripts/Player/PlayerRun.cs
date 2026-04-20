using System;
using Inputs;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerRun : NetworkBehaviour, ISpeedModifier
    {
        public event Action<bool> OnRunEvent;
        
        [SerializeField] private PlayerState playerState;
        [SerializeField] private InputReader inputReader;
        [SerializeField] private float speedModifier = 2f;
        
        [SerializeField] private float staminaMax;
        [SerializeField] private float staminaDrainPerSecond;
        [SerializeField] private float staminaGainPerSecond;
        [SerializeField] private float staminaCooldownThreshold = 5f;
        
        private float _stamina;
        private bool _isExhausted;
        private bool _isRunning;
        private bool _isDead;
        
        
        public override void OnNetworkSpawn()
        {
            _stamina = staminaMax;
            
            if (IsOwner)
            {
                inputReader.OnRunEvent += InputReader_OnRunEvent;
                playerState.OnPlayerDead += PlayerState_OnPlayerDead;
            }
            
        }
        

        private void InputReader_OnRunEvent(bool isRunning) => _isRunning = isRunning;
        
        private void PlayerState_OnPlayerDead(bool isDead)
        {
            _isDead = isDead;
            
            if (_isDead)
            {
                OnRunEvent?.Invoke(false);
            }
        }
        
        
        private void Update()
        {
            if (IsOwner && !_isDead)
            {
                UpdateStamina(_isRunning); 
            }
        }
        
        private void UpdateStamina(bool isRunning)
        {
            bool canRun = isRunning && !_isExhausted;
            OnRunEvent?.Invoke(canRun);
            
            if (canRun)
            {
                _stamina -= staminaDrainPerSecond * Time.deltaTime;

                if (_stamina <= 0f)
                {
                    _stamina = 0f;
                    _isExhausted = true;
                }
            }
            else
            {
                _stamina += staminaGainPerSecond * Time.deltaTime;
                _stamina = Mathf.Min(_stamina, staminaMax);

                if (_isExhausted && _stamina >= staminaCooldownThreshold)
                {
                    _isExhausted = false;
                }
            }
        }
        
        public float ModifySpeed(float baseSpeed)
        {
            if (_isRunning && !_isExhausted)
            {
                return baseSpeed * speedModifier;
            }

            return baseSpeed;
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnRunEvent -= InputReader_OnRunEvent;
                playerState.OnPlayerDead -= PlayerState_OnPlayerDead;
            }
        }
        
    }
}

