using System.Collections.Generic;
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

        // Which object sits in the slot, so an item can tell on every peer that it is on a totem.
        private readonly NetworkVariable<NetworkObjectReference> _slotPickable = new();

        private static readonly List<ItemSlotTotem> SpawnedTotems = new();

        private NetworkObject _currentPickable;
        private int _currentItemId = EMPTY_SLOT;


        public override void OnNetworkSpawn()
        {
            _slotItemId.OnValueChanged += SlotItemId_OnValueChanged;
            UpdatePlacementArea();
            SpawnedTotems.Add(this);
        }

        public override void OnNetworkDespawn()
        {
            _slotItemId.OnValueChanged -= SlotItemId_OnValueChanged;
            SpawnedTotems.Remove(this);
        }

        /// <summary>The totem whose slot holds this item, on every peer; null when it is on none.</summary>
        public static ItemSlotTotem Holding(NetworkObject item)
        {
            if (item == null) return null;

            foreach (ItemSlotTotem totem in SpawnedTotems)
            {
                if (totem == null || !totem.IsOccupied) continue;
                if (totem._slotPickable.Value.NetworkObjectId == item.NetworkObjectId) return totem;
            }

            return null;
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

        /// <summary>
        /// What TryDeposit checks, short of the slot being free. A swap asks first, so the hand only gives
        /// its item up when the slot will take it.
        /// </summary>
        public bool CanDeposit(ulong clientId, int itemId)
        {
            if (!CheckOwnership(clientId)) return false;

            return itemDatabase.GetItem(itemId) != null;
        }

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
                _slotPickable.Value = netObj;

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
            _slotPickable.Value = default;
        }

    }
}
