using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    /// <summary>
    /// Who is ready in the lobby, synced to every peer so the ready board can list players by
    /// name. When everybody is ready the server runs a short countdown before loading the match;
    /// anyone backing out (or the host switching contracts) cancels it.
    /// </summary>
    public class PlayersReady : NetworkBehaviour
    {
        /// <summary>(ready, expected) on every peer, whenever either changes.</summary>
        public event Action<int, int> OnReadyCountChanged;

        /// <summary>Any change worth redrawing: ready list, expected count or countdown.</summary>
        public event Action OnStateChanged;

        [SerializeField] private Loader.Scene sceneToLoad;
        [SerializeField] private SceneTransition sceneTransition;

        [Tooltip("Seconds between everybody being ready and the match loading. 0 loads at once.")]
        [SerializeField, Min(0f)] private float countdownSeconds = 5f;

        private readonly NetworkList<ulong> _readyClients = new NetworkList<ulong>();
        private readonly NetworkVariable<int> _expectedCount = new NetworkVariable<int>();

        // Server time when the match loads; negative while no countdown runs.
        private readonly NetworkVariable<double> _countdownEnd = new NetworkVariable<double>(-1d);

        private bool _subscribedToTracker;
        private bool _isSceneLoading;

        public int ExpectedCount => _expectedCount.Value;
        public bool IsCountingDown => _countdownEnd.Value >= 0d;
        public float CountdownSeconds => countdownSeconds;

        /// <summary>Seconds left before the match loads, or -1 when no countdown runs.</summary>
        public float CountdownRemaining
        {
            get
            {
                if (!IsCountingDown || NetworkManager == null) return -1f;

                double left = _countdownEnd.Value - NetworkManager.ServerTime.Time;
                return Mathf.Max(0f, (float)left);
            }
        }

        public bool IsReady(ulong clientId) => _readyClients.Contains(clientId);

        public override void OnNetworkSpawn()
        {
            _readyClients.OnListChanged += ReadyClients_OnListChanged;
            _expectedCount.OnValueChanged += ExpectedCount_OnValueChanged;
            _countdownEnd.OnValueChanged += CountdownEnd_OnValueChanged;

            if (!IsServer) return;

            BindToPlayerTracker();
            RebuildFromTracker();
            CheckAllReady();
        }

        private void BindToPlayerTracker()
        {
            if (_subscribedToTracker || PlayerTracker.Instance == null) return;

            PlayerTracker.Instance.OnPlayerConnected += HandlePlayerConnected;
            PlayerTracker.Instance.OnPlayerDisconnected += HandlePlayerDisconnected;
            PlayerTracker.Instance.OnExpectedPlayerCountChanged += HandleExpectedCountChanged;
            _subscribedToTracker = true;
        }

        private void RebuildFromTracker()
        {
            _readyClients.Clear();
            _expectedCount.Value = PlayerTracker.Instance != null ? PlayerTracker.Instance.ExpectedPlayerCount : 0;
        }

        private void HandlePlayerConnected(ulong clientId)
        {
            _readyClients.Remove(clientId);
            CheckAllReady();
        }

        private void HandlePlayerDisconnected(ulong clientId)
        {
            _readyClients.Remove(clientId);
            CheckAllReady();
        }

        private void HandleExpectedCountChanged(int count)
        {
            _expectedCount.Value = count;
            CheckAllReady();
        }

        public void SetPlayerReadyServer(ulong clientId)
        {
            if (!IsServer || _isSceneLoading) return;

            if (!_readyClients.Contains(clientId))
            {
                _readyClients.Add(clientId);
            }

            CheckAllReady();
        }

        public void SetPlayerNotReadyServer(ulong clientId)
        {
            if (!IsServer || _isSceneLoading) return;

            _readyClients.Remove(clientId);
            CheckAllReady();
        }

        /// <summary>Everybody back to not ready, e.g. the host switched contracts.</summary>
        public void ResetAllReadyServer()
        {
            if (!IsServer || _isSceneLoading) return;

            _readyClients.Clear();
            CheckAllReady();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetPlayerReadyRpc(RpcParams rpcParams = default)
        {
            SetPlayerReadyServer(rpcParams.Receive.SenderClientId);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetPlayerNotReadyRpc(RpcParams rpcParams = default)
        {
            SetPlayerNotReadyServer(rpcParams.Receive.SenderClientId);
        }

        private void CheckAllReady()
        {
            if (!IsServer || PlayerTracker.Instance == null || _isSceneLoading) return;

            _expectedCount.Value = PlayerTracker.Instance.ExpectedPlayerCount;

            if (AreAllReady())
            {
                if (IsCountingDown) return;

                if (countdownSeconds <= 0f)
                {
                    BeginLoad();
                    return;
                }

                _countdownEnd.Value = NetworkManager.ServerTime.Time + countdownSeconds;
            }
            else if (IsCountingDown)
            {
                _countdownEnd.Value = -1d;
            }
        }

        private bool AreAllReady()
        {
            int total = PlayerTracker.Instance.ExpectedPlayerCount;
            int connected = PlayerTracker.Instance.ConnectedPlayerCount;

            return total > 0 && connected >= total && CountReadyConnected() >= total;
        }

        private int CountReadyConnected()
        {
            if (PlayerTracker.Instance == null) return _readyClients.Count;

            int ready = 0;

            foreach (ulong id in PlayerTracker.Instance.ConnectedPlayers)
            {
                if (_readyClients.Contains(id)) ready++;
            }

            return ready;
        }

        private void Update()
        {
            if (!IsServer || _isSceneLoading || !IsCountingDown) return;

            if (NetworkManager.ServerTime.Time >= _countdownEnd.Value)
            {
                BeginLoad();
            }
        }

        private void BeginLoad()
        {
            _isSceneLoading = true;

            StartFadeAndLoadClientRpc();
            StartCoroutine(LoadSceneRoutine());
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void StartFadeAndLoadClientRpc()
        {
            StartCoroutine(sceneTransition.FadeOut());
        }

        private IEnumerator LoadSceneRoutine()
        {
            yield return new WaitForSeconds(sceneTransition.FadeDuration);
            Loader.LoadNetwork(sceneToLoad);
        }

        private void ReadyClients_OnListChanged(NetworkListEvent<ulong> change) => RaiseChanged();

        private void ExpectedCount_OnValueChanged(int previous, int current) => RaiseChanged();

        private void CountdownEnd_OnValueChanged(double previous, double current) => OnStateChanged?.Invoke();

        private void RaiseChanged()
        {
            (int ready, int total) = GetReadyCount();
            OnReadyCountChanged?.Invoke(ready, total);
            OnStateChanged?.Invoke();
        }

        public (int ready, int total) GetReadyCount()
        {
            return (_readyClients.Count, _expectedCount.Value);
        }

        public override void OnNetworkDespawn()
        {
            _readyClients.OnListChanged -= ReadyClients_OnListChanged;
            _expectedCount.OnValueChanged -= ExpectedCount_OnValueChanged;
            _countdownEnd.OnValueChanged -= CountdownEnd_OnValueChanged;

            if (PlayerTracker.Instance != null && _subscribedToTracker)
            {
                PlayerTracker.Instance.OnPlayerConnected -= HandlePlayerConnected;
                PlayerTracker.Instance.OnPlayerDisconnected -= HandlePlayerDisconnected;
                PlayerTracker.Instance.OnExpectedPlayerCountChanged -= HandleExpectedCountChanged;
                _subscribedToTracker = false;
            }
        }
    }
}
