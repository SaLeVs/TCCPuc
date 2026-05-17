using System;
using Missions;
using Unity.Netcode;
using UnityEngine;

namespace Audience
{
    public class AudienceManager : NetworkBehaviour
    {
        public static AudienceManager Instance { get; private set; }
        
        public event Action<float> OnAudienceChanged;
        public event Action<float> OnAudienceGained;
        public event Action<float> OnAudienceLost;
        
        [SerializeField] private MissionManager missionManager;
        
        [SerializeField] private float startingAudience;
        [SerializeField] private float idleThresholdBeforeDecay = 30f;
        [SerializeField] private float decayRatePerSecond = 3f;
        [SerializeField] private float globalGainMultiplier = 1f;
        [SerializeField] private float maxGainPerSecond = 100f;
        
        private NetworkVariable<float> _audience = new NetworkVariable<float>(0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public float CurrentAudience => _audience.Value;
        public float MaxAudience => _maxAudience;
        public float NormalizedAudience => _maxAudience > 0f ? _audience.Value / _maxAudience : 0f;
        public float IdleTimer => _idleTimer;
        public bool IsDecaying => _idleTimer >= idleThresholdBeforeDecay;
        
        private float _maxAudience;
        private float _pendingGainThisFrame;
        private bool _receivedGainThisFrame;
        private bool _receivedPresenceThisFrame;
        private float _idleTimer;
        
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _audience.Value = startingAudience;
            }

            _maxAudience = missionManager.CurrentContract.maxAudience;
            _audience.OnValueChanged += HandleAudienceValueChanged;
        }
        
        
        private void HandleAudienceValueChanged(float previous, float current) => OnAudienceChanged?.Invoke(current);
        
        private void Update()
        {
            if (IsServer)
            {
                ServerTick();
            }
        }
        
        private void ServerTick()
        {
            float deltaTime = Time.deltaTime;

            if (_receivedGainThisFrame)
            {
                float audienceGain = Mathf.Min(_pendingGainThisFrame * globalGainMultiplier, maxGainPerSecond * deltaTime);

                AddAudience(audienceGain);

                _idleTimer = 0f;
                _receivedGainThisFrame = false;
                _receivedPresenceThisFrame = false;
                _pendingGainThisFrame = 0f;
            }
            else if (_receivedPresenceThisFrame)
            {
                _idleTimer = 0f;
                _receivedPresenceThisFrame = false;
            }
            else
            {
                _idleTimer += deltaTime;

                if (_idleTimer >= idleThresholdBeforeDecay)
                {
                    RemoveAudience(decayRatePerSecond * deltaTime);
                }
            }
        }
        
        public void SubmitGain(float gainPerSecond)
        {
            if (IsServer)
            {
                _pendingGainThisFrame  += gainPerSecond * Time.deltaTime;
                _receivedGainThisFrame  = true;
            }
        }
        
        public void SubmitPresence()
        {
            if (IsServer)
            {
                _receivedPresenceThisFrame = true;
            }
        }
        
        private void AddAudience(float amount)
        {
            if (amount <= 0f) return;
            
            float before = _audience.Value;
            _audience.Value = Mathf.Clamp(_audience.Value + amount, 0f, _maxAudience);
            float audienceAmount = _audience.Value - before;
            
            if (audienceAmount > 0f)
            {
                NotifyGainClientRpc(audienceAmount);
            }
        }

        private void RemoveAudience(float amount)
        {
            if (amount <= 0f) return;
            
            float before = _audience.Value;
            _audience.Value = Mathf.Clamp(_audience.Value - amount, 0f, _maxAudience);
            float audienceAmount = before - _audience.Value;
            
            if (audienceAmount > 0f)
            {
                NotifyLossClientRpc(audienceAmount);
            }
        }

        [Rpc(SendTo.ClientsAndHost)] private void NotifyGainClientRpc(float delta) => OnAudienceGained?.Invoke(delta);
        [Rpc(SendTo.ClientsAndHost)] private void NotifyLossClientRpc(float delta) => OnAudienceLost?.Invoke(delta);
        
        
        public override void OnNetworkDespawn()
        {
            _audience.OnValueChanged -= HandleAudienceValueChanged;
        }
        
    }
}