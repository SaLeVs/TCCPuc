using System;
using Interfaces;
using Missions;
using Missions.Donations;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Audience
{
    public class AudienceManager : NetworkBehaviour, IAudienceProvider
    {
        public static AudienceManager Instance { get; private set; }

        public event Action<float> OnAudienceChanged;
        public event Action<float> OnAudienceGained;
        public event Action<float> OnAudienceLost;
        public event Action OnAudienceThresholdReached;

        [SerializeField] private MissionManager missionManager;

        [SerializeField] private float startingAudience;
        [SerializeField] private float idleThresholdBeforeDecay = 30f;
        [SerializeField] private float decayRatePerSecond = 3f;
        [SerializeField] private float globalGainMultiplier = 1f;
        [SerializeField] private float maxGainPerSecond = 100f;
        [SerializeField, Range(0f, 1f)] private float escapeThreshold = 0.75f;

        private NetworkVariable<float> _audience = new NetworkVariable<float>(0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public float CurrentAudience => _audience.Value;
        public float MaxAudience => _maxAudience;
        public float NormalizedAudience => _maxAudience > 0f ? _audience.Value / _maxAudience : 0f;
        public float IdleTimer => _idleTimer;
        public bool IsDecaying => _idleTimer >= idleThresholdBeforeDecay;

        private float _maxAudience;
        private float _pendingGain;
        private bool _receivedGainThisFrame;
        private bool _receivedPresenceThisFrame;
        private float _idleTimer;
        private bool  _thresholdReached;


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }


        /// <summary>
        /// Subscribes before reading the contract, and reads it defensively.
        ///
        /// <para>The ceiling used to be read on the line above the subscription, straight off
        /// <c>missionManager.CurrentContract</c>. A peer where either reference was missing threw
        /// there and so never subscribed at all - its bar then sat on the starting number for the
        /// whole match while the server kept counting. A missing contract also left the ceiling at
        /// zero, and a zero ceiling clamps every gain back to nothing.</para>
        /// </summary>
        public override void OnNetworkSpawn()
        {
            _audience.OnValueChanged += AudienceManager_OnAudienceChanged;

            _maxAudience = ResolveMaxAudience();

            if (IsServer)
            {
                _audience.Value = Mathf.Clamp(startingAudience, 0f, _maxAudience);
            }
        }

        private float ResolveMaxAudience()
        {
            ContractsSO contract = missionManager != null ? missionManager.CurrentContract : null;

            if (contract != null && contract.maxAudience > 0f) return contract.maxAudience;

            Debug.LogError($"{nameof(AudienceManager)}: no contract to take a ceiling from, so every " +
                           "gain would clamp back to zero. Falling back to the starting value - " +
                           "assign the mission manager and give the contract a maxAudience.", this);

            return Mathf.Max(startingAudience, 1f);
        }


        private void AudienceManager_OnAudienceChanged(float previous, float current) => OnAudienceChanged?.Invoke(current);

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
                // maxGainPerSecond was declared and never read. It is a rate, so what it holds back
                // is carried into the next frame rather than dropped: a burst of gain arrives late
                // instead of quietly costing the players viewers they already earned.
                float ceiling = maxGainPerSecond > 0f ? maxGainPerSecond * deltaTime : _pendingGain;
                float granted = Mathf.Min(_pendingGain, ceiling);

                // Cleared before the add, not after. AddAudience reaches into other systems, and an
                // exception thrown in there used to leave the pending gain armed - the same viewers
                // were then granted again on every following frame.
                _pendingGain -= granted;
                _receivedGainThisFrame = _pendingGain > 0f;
                _receivedPresenceThisFrame = false;
                _idleTimer = 0f;

                AddAudience(granted);
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

        public void SubmitGain(float gain)
        {
            if (!IsServer) return;
            if (gain <= 0f) return;

            _pendingGain += gain * globalGainMultiplier;
            _receivedGainThisFrame = true;
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
                PublishViewerCount();
                NotifyGainClientRpc(audienceAmount);
            }

            if (!_thresholdReached && NormalizedAudience >= escapeThreshold)
            {
                _thresholdReached = true;
                OnAudienceThresholdReached?.Invoke();
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
                PublishViewerCount();
                NotifyLossClientRpc(audienceAmount);
            }
        }

        /// <summary>
        /// The donation manager is a spawned object of its own, so it can be missing for a frame at
        /// the start of a match and already gone at the end of one. Reaching through it unguarded
        /// from inside the server tick is what turned that gap into a thrown frame.
        /// </summary>
        private void PublishViewerCount()
        {
            if (DonationManager.Instance == null) return;

            DonationManager.Instance.SetViewerCount((int)_audience.Value);
        }

        [Rpc(SendTo.ClientsAndHost)] private void NotifyGainClientRpc(float delta) => OnAudienceGained?.Invoke(delta);
        [Rpc(SendTo.ClientsAndHost)] private void NotifyLossClientRpc(float delta) => OnAudienceLost?.Invoke(delta);


        public override void OnNetworkDespawn()
        {
            _audience.OnValueChanged -= AudienceManager_OnAudienceChanged;
        }

    }
}
