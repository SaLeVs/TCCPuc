using Interfaces;
using Unity.Netcode;
using UnityEngine;
using ScriptableObjects;

namespace Missions.Puzzles
{
    public class ItemSlotTotem : PuzzlePieceBase, IInteractable
    {
        private const int EMPTY_SLOT = -1;

        [SerializeField] private ItemDataSO expectedItem;

        [Tooltip("Where the deposited item appears - and where the placement ghost previews it, so the " +
                 "two can never disagree.")]
        [SerializeField] private Transform spawnPoint;

        [SerializeField] private ItemListSO itemDatabase;

        [Tooltip("Trigger covering where the placement ghost appears, so aiming at the preview works like " +
                 "aiming at the slot. Off while the slot is full, or it would stand between the player and " +
                 "the item they want to pick back up.")]
        [SerializeField] private Collider placementArea;

        public override bool IsCorrect => HasItemInSlot() && _currentItemId == expectedItem.itemId;

        public Transform PlacementPoint => spawnPoint;

        /// <summary>Whether something is in the slot, on every peer.</summary>
        public bool IsOccupied => HasItemInSlot();

        // The item in the slot lives only on the server; this is how clients know the slot is taken.
        // Without it a client's CanInteract always read the slot as empty.
        private readonly NetworkVariable<int> _slotItemId = new(EMPTY_SLOT);

        private NetworkObject _currentPickable;
        private int _currentItemId = EMPTY_SLOT;


        public override void OnNetworkSpawn()
        {
            _slotItemId.OnValueChanged += SlotItemId_OnValueChanged;
            UpdatePlacementArea();
        }

        public override void OnNetworkDespawn()
        {
            _slotItemId.OnValueChanged -= SlotItemId_OnValueChanged;
        }

        private void SlotItemId_OnValueChanged(int previous, int current) => UpdatePlacementArea();

        private void UpdatePlacementArea()
        {
            if (placementArea != null) placementArea.enabled = _slotItemId.Value == EMPTY_SLOT;
        }

        public bool CanInteract(GameObject interactor)
        {
            if (HasItemInSlot()) return false;
            if (!interactor.TryGetComponent(out NetworkObject networkObject)) return false;

            return CheckOwnership(networkObject.OwnerClientId);
        }

        public bool Interact(GameObject playerInteractor) => false;

        public bool TryDeposit(ulong clientId, int itemId)
        {
            if (!CheckOwnership(clientId)) return false;
            if (HasItemInSlot()) return false;

            ItemDataSO item = itemDatabase.GetItem(itemId);

            if (item == null) return false;

            GameObject spawned = Instantiate(item.prefabPickable, spawnPoint.position, spawnPoint.rotation);

            if (spawned.TryGetComponent(out NetworkObject netObj))
            {
                netObj.Spawn();
                _currentPickable = netObj;

                if (spawned.TryGetComponent(out IMissionOwnerAware ownerAware))
                {
                    ownerAware.BindToPuzzle(Manager);
                }
            }

            _currentItemId = itemId;
            _slotItemId.Value = itemId;
            NotifyChanged(clientId);
            return true;
        }

        private void Update()
        {
            // The item can be picked back up. The server notices here rather than the next time
            // someone happens to aim at the slot, so every client sees it empty straight away.
            if (IsServer && _slotItemId.Value != EMPTY_SLOT) HasItemInSlot();
        }

        private bool HasItemInSlot()
        {
            if (!IsServer) return _slotItemId.Value != EMPTY_SLOT;

            if (_currentPickable != null && _currentPickable.IsSpawned) return true;

            if (_currentItemId != EMPTY_SLOT) ClearSlot();

            return false;
        }

        private void ClearSlot()
        {
            _currentPickable = null;
            _currentItemId = EMPTY_SLOT;
            _slotItemId.Value = EMPTY_SLOT;
        }

    }
}
