using Components.Sound;
using Interfaces;
using Missions;
using Missions.Puzzles;
using Objects.UsableItems;
using Player;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Objects.PickupItems
{
    public class ItemPickable : NetworkBehaviour, IInteractable, IMissionOwnerAware
    {
        [SerializeField] private ItemDataSO itemData;

        [SerializeField] private SoundDefinitionSO pickupSound;

        private MissionOwnershipFilter _ownershipFilter;

        public int ItemId => itemData.itemId;
        
        private void Awake()
        {
            _ownershipFilter = GetComponent<MissionOwnershipFilter>();
        }
        

        public void BindToPuzzle(PuzzleManagerBase puzzle)
        {
            _ownershipFilter?.SetManager(puzzle);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (!interactor.TryGetComponent(out NetworkObject networkObject)) return false;
            if (_ownershipFilter != null && !_ownershipFilter.CanClientInteract(networkObject.OwnerClientId)) return false;

            // Asked on the interacting player's own machine, where the selected slot is known.
            return !interactor.TryGetComponent(out PlayerInventory playerInventory)
                   || TotemAllowsTaking(playerInventory, playerInventory.CurrentSelectedSlot, out _);
        }

        public bool Interact(GameObject playerInteractor)
        {
            if (!CanInteract(playerInteractor)) return false;
            if (!playerInteractor.TryGetComponent(out NetworkObject networkObject)) return false;

            // Only the player's own machine knows which slot is in the hand, so it travels with the request.
            int selectedSlot = playerInteractor.TryGetComponent(out PlayerInventory playerInventory)
                ? playerInventory.CurrentSelectedSlot
                : -1;

            if (!IsServer)
            {
                InteractServerRpc(networkObject, selectedSlot);
                return true;
            }

            TryInteract(networkObject, selectedSlot);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void InteractServerRpc(NetworkObjectReference playerRef, int selectedSlot)
        {
            if (!playerRef.TryGet(out NetworkObject playerNetworkObject)) return;

            TryInteract(playerNetworkObject, selectedSlot);
        }

        private void TryInteract(NetworkObject playerNetworkObject, int selectedSlot)
        {
            if (_ownershipFilter != null && !_ownershipFilter.CanClientInteract(playerNetworkObject.OwnerClientId))
            {
                return;
            }

            AddItemToInventory(playerNetworkObject.gameObject, selectedSlot);
        }

        private void AddItemToInventory(GameObject playerInteractor, int selectedSlot)
        {
            if (!playerInteractor.TryGetComponent(out PlayerInventory playerInventory)) return;

            if (playerInventory.HasInventorySpace())
            {
                playerInventory.TryAddItemServer(itemData.itemId);
                RemoveItem();
                return;
            }

            if (!TotemAllowsTaking(playerInventory, selectedSlot, out ItemSlotTotem totem)) return;

            if (totem != null)
            {
                SwapOnTotem(playerInventory, selectedSlot, totem);
                return;
            }

            // Full inventory: swap with the item in the hand, if the hand holds one.
            if (playerInventory.GetItemInSlot(selectedSlot - 1) != -1)
            {
                NetworkObject droppedItem = playerInventory.ReplaceSelectedItemServer(selectedSlot, itemData.itemId, transform.position, transform.rotation);
                BindToItsPuzzle(droppedItem);
                RemoveItem();
            }
        }

        /// <summary>
        /// With a full inventory, taking this item trades it for the one in the hand, which is left where
        /// this one was. On a totem it would stand there without counting, so there only another box the
        /// totem takes may trade places with it. With room in the inventory, or off a totem, nothing changes.
        /// </summary>
        private bool TotemAllowsTaking(PlayerInventory playerInventory, int selectedSlot, out ItemSlotTotem totem)
        {
            totem = null;

            if (playerInventory.HasInventorySpace()) return true;

            totem = ItemSlotTotem.Holding(NetworkObject);

            if (totem == null) return true;

            ItemDataSO heldItem = playerInventory.GetItemDataInSlot(selectedSlot - 1);

            if (heldItem == null || heldItem.prefabUsable == null) return false;
            if (!heldItem.prefabUsable.TryGetComponent(out SlotPlaceableItem _)) return false;

            return totem.CanDeposit(playerInventory.OwnerClientId, heldItem.itemId);
        }

        // The box in the hand goes onto the totem in this one's place, exactly as if it had been placed
        // there, and this one goes to the hand. TotemAllowsTaking has made sure the totem takes it.
        private void SwapOnTotem(PlayerInventory playerInventory, int selectedSlot, ItemSlotTotem totem)
        {
            int heldItemId = playerInventory.GetItemInSlot(selectedSlot - 1);

            if (!playerInventory.SetItemInSlotServer(selectedSlot - 1, itemData.itemId)) return;

            RemoveItem();
            totem.TryDeposit(playerInventory.OwnerClientId, heldItemId);
        }

        // The item that leaves the hand is spawned anew and knows nothing of its puzzle, so not even
        // the mission owner could pick it up again. It gets the puzzle back the way a dead player's
        // dropped items do.
        private static void BindToItsPuzzle(NetworkObject droppedItem)
        {
            if (droppedItem == null) return;
            if (!droppedItem.TryGetComponent(out IMissionOwnerAware ownerAware)) return;
            if (MissionItemRegistry.Instance == null) return;
            if (!MissionItemRegistry.Instance.TryGetManager(ownerAware.ItemId, out PuzzleManagerBase puzzle)) return;

            ownerAware.BindToPuzzle(puzzle);
        }

        private void RemoveItem()
        {
            if (!IsServer)
                return;

            // No emitter: this object is despawned on the next line, before the sound reaches anyone.
            WorldSound.Play(pickupSound, transform.position);

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn();
            }
        }
    }
}