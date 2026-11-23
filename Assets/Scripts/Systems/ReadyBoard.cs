using System.Collections.Generic;
using Interfaces;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;
using Unity.Cinemachine;

namespace Systems
{
    /// <summary>
    /// The lobby board where the match starts: the host picks the contract with the arrows,
    /// everybody reads the goal and the objectives, sees who is ready and marks themselves
    /// ready. The pick is networked and lands in <see cref="ContractSelection"/> on every peer.
    /// </summary>
    public class ReadyBoard : NetworkBehaviour, IInteractable
    {
        [Header("Ready System")]
        [SerializeField] private PlayersReady playersReady;

        [Header("Contracts")]
        [SerializeField] private ContractCatalogSO contractCatalog;

        [Header("View")]
        [SerializeField] private ReadyBoardView view;
        [Tooltip("Seconds between roster refreshes (names arrive after the players spawn).")]
        [SerializeField, Min(0.1f)] private float rosterRefreshInterval = 0.5f;

        [Header("Camera")]
        [SerializeField] private CinemachineCamera readyBoardVCam;

        private readonly NetworkVariable<int> _contractIndex = new NetworkVariable<int>();
        private readonly List<ReadyBoardView.RosterEntry> _roster = new();
        private readonly List<ulong> _playerIds = new();

        private IInputLockable _currentInputLockable;

        private bool _isInUse;
        private float _nextRosterRefresh;

        public override void OnNetworkSpawn()
        {
            playersReady.OnStateChanged += PlayersReady_OnStateChanged;
            _contractIndex.OnValueChanged += ContractIndex_OnValueChanged;

            if (IsServer)
            {
                // Back in the lobby after a match: keep the last pick on the board.
                _contractIndex.Value = FindCatalogIndex(ContractSelection.Selected);
            }

            ApplyContract();
            RefreshRoster();
            view.ShowCountdown(playersReady.CountdownRemaining);
        }

        public override void OnNetworkDespawn()
        {
            if (playersReady != null)
            {
                playersReady.OnStateChanged -= PlayersReady_OnStateChanged;
            }

            _contractIndex.OnValueChanged -= ContractIndex_OnValueChanged;
        }

        private void Update()
        {
            if (!IsSpawned) return;

            view.ShowCountdown(playersReady.CountdownRemaining);

            if (Time.unscaledTime >= _nextRosterRefresh)
            {
                RefreshRoster();
            }
        }

        // ---------- Contract ----------

        public void PreviousContract() => ChangeContractRpc(-1);

        public void NextContract() => ChangeContractRpc(1);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ChangeContractRpc(int step, RpcParams rpcParams = default)
        {
            // Host only, for now. A vote could come later.
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            if (contractCatalog == null || contractCatalog.Count <= 1) return;

            _contractIndex.Value = contractCatalog.Wrap(_contractIndex.Value + step);

            // Nobody stays committed to a contract they didn't see.
            playersReady.ResetAllReadyServer();
        }

        private void ContractIndex_OnValueChanged(int previous, int current) => ApplyContract();

        private void ApplyContract()
        {
            int count = contractCatalog != null ? contractCatalog.Count : 0;
            int index = contractCatalog != null ? contractCatalog.Wrap(_contractIndex.Value) : 0;
            ContractsSO contract = contractCatalog != null ? contractCatalog.Get(index) : null;

            ContractSelection.Selected = contract;
            view.ShowContract(contract, index, count, IsHost);
        }

        private int FindCatalogIndex(ContractsSO contract)
        {
            if (contract == null || contractCatalog == null) return 0;

            for (int i = 0; i < contractCatalog.Count; i++)
            {
                if (contractCatalog.Get(i) == contract) return i;
            }

            return 0;
        }

        // ---------- Ready / roster ----------

        private void PlayersReady_OnStateChanged() => RefreshRoster();

        private void RefreshRoster()
        {
            _nextRosterRefresh = Time.unscaledTime + rosterRefreshInterval;

            NetworkManager nm = NetworkManager;
            if (nm == null) return;

            _roster.Clear();
            _playerIds.Clear();

            // Player objects exist on every peer, so this works the same on host and clients.
            foreach (NetworkObject netObj in nm.SpawnManager.SpawnedObjectsList)
            {
                if (netObj == null || !netObj.IsPlayerObject) continue;
                if (_playerIds.Contains(netObj.OwnerClientId)) continue;

                _playerIds.Add(netObj.OwnerClientId);
            }

            _playerIds.Sort();

            int readyCount = 0;

            foreach (ulong id in _playerIds)
            {
                bool ready = playersReady.IsReady(id);
                if (ready) readyCount++;

                _roster.Add(new ReadyBoardView.RosterEntry
                {
                    Name = ResolveName(nm, id),
                    IsHost = id == NetworkManager.ServerClientId,
                    IsLocal = id == nm.LocalClientId,
                    State = ready ? ReadyBoardRosterRow.State.Ready : ReadyBoardRosterRow.State.Waiting,
                });
            }

            for (int i = _playerIds.Count; i < playersReady.ExpectedCount; i++)
            {
                _roster.Add(new ReadyBoardView.RosterEntry { State = ReadyBoardRosterRow.State.Connecting });
            }

            view.ShowRoster(_roster, readyCount, playersReady.ExpectedCount);
            view.ShowLocalReady(playersReady.IsReady(nm.LocalClientId));
        }

        private static string ResolveName(NetworkManager nm, ulong clientId)
        {
            foreach (NetworkObject netObj in nm.SpawnManager.SpawnedObjectsList)
            {
                if (netObj == null || !netObj.IsPlayerObject || netObj.OwnerClientId != clientId) continue;

                if (netObj.TryGetComponent(out IPlayerDisplayName named) && !string.IsNullOrWhiteSpace(named.DisplayName))
                {
                    return named.DisplayName;
                }
            }

            return $"Jogador {clientId + 1}";
        }

        public void SetPlayerReady()
        {
            if (NetworkManager == null) return;

            if (playersReady.IsReady(NetworkManager.LocalClientId))
            {
                playersReady.SetPlayerNotReadyRpc();
            }
            else
            {
                playersReady.SetPlayerReadyRpc();
            }
        }

        // ---------- Interaction ----------

        public bool CanInteract(GameObject interactor)
        {
            return !_isInUse;
        }

        public bool Interact(GameObject playerInteractor)
        {
            if (!playerInteractor.TryGetComponent(out NetworkObject netObj)) return false;
            if (!netObj.IsOwner) return false;
            if (!playerInteractor.TryGetComponent(out IInputLockable lockable)) return false;

            _isInUse = true;
            _currentInputLockable = lockable;

            _currentInputLockable.SetInputLocked(InputLockReason.ReadyBoard, true);
            readyBoardVCam.Priority = 20;

            return true;
        }

        public void Exit()
        {
            _isInUse = false;

            _currentInputLockable?.SetInputLocked(InputLockReason.ReadyBoard, false);
            _currentInputLockable = null;

            readyBoardVCam.Priority = 0;
        }
    }
}
