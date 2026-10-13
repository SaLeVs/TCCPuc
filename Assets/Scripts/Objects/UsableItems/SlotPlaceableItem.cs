using Inputs;
using Interfaces;
using Missions.Puzzles;
using Player;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Objects.UsableItems
{
    public class SlotPlaceableItem : NetworkBehaviour, IUsable
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private ItemDataSO itemData;

        [Header("Placement ghost")]
        [Tooltip("See-through material for the copy of this item shown on a slot before it is placed.")]
        [SerializeField] private Material ghostMaterial;

        [SerializeField] private Color ghostValidColor = new(0.25f, 1f, 0.4f, 0.35f);

        [Tooltip("The slot is someone else's mission - the same case the HUD marks UNAVAILABLE.")]
        [SerializeField] private Color ghostBlockedColor = new(1f, 0.28f, 0.22f, 0.35f);

        [Tooltip("Seconds the ghost stays hidden after placing, while the server spawns the real item.")]
        [SerializeField, Min(0f)] private float ghostHideAfterPlace = 0.6f;

        private GameObject _playerInteractor;
        private PlayerInteractor _interactor;
        private PlayerState _playerState;
        private PlacementGhost _ghost;
        private float _ghostHiddenUntil;

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnUseEvent += InputReader_OnUseEvent;

                NetworkObject playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(OwnerClientId);

                if (playerNetworkObject != null)
                {
                    _playerInteractor = playerNetworkObject.gameObject;
                    _playerInteractor.TryGetComponent(out _interactor);
                    _playerInteractor.TryGetComponent(out _playerState);
                }
            }
        }

        private void InputReader_OnUseEvent()
        {
            Use(_playerInteractor);
        }

        public bool CanUse(GameObject playerInteractor)
        {
            if (playerInteractor.TryGetComponent(out PlayerState playerState) && playerState.IsDead) return false;

            return _interactor?.CurrentInteractable is ItemSlotTotem;
        }

        public void Use(GameObject playerInteractor)
        {
            if (!CanUse(playerInteractor)) return;

            if (_interactor.CurrentInteractable is ItemSlotTotem totem)
            {
                TryPlaceServerRpc(totem.NetworkObjectId);

                // The real item takes a round trip to appear; the ghost leaves now so the two never
                // stand in the same spot.
                _ghostHiddenUntil = Time.time + ghostHideAfterPlace;
                HideGhost();
            }
        }

        [Rpc(SendTo.Server)]
        private void TryPlaceServerRpc(ulong totemNetworkId)
        {
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(totemNetworkId, out NetworkObject netObj)) return;
            if (!netObj.TryGetComponent(out ItemSlotTotem totem)) return;
            if (!totem.TryDeposit(OwnerClientId, itemData.itemId)) return;

            NetworkObject playerNetObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(OwnerClientId);
            if (playerNetObj == null) return;

            if (playerNetObj.TryGetComponent(out PlayerInventory inventory))
            {
                inventory.TryRemoveItemServer(itemData.itemId);
            }

        }

        private void LateUpdate()
        {
            if (!IsOwner) return;

            ItemSlotTotem totem = GhostTarget();

            if (totem == null)
            {
                HideGhost();
                return;
            }

            if (_ghost == null)
            {
                _ghost = PlacementGhost.Build(itemData.prefabPickable, ghostMaterial);
            }

            // Green where placing will work, red where the slot refuses this player - the outline and
            // the HUD's UNAVAILABLE prompt read the same flag.
            _ghost.Show(totem.PlacementPoint, _interactor.IsHoveredBlocked ? ghostBlockedColor : ghostValidColor);
        }

        /// <summary>The slot the crosshair rests on, if it should show this item's ghost.</summary>
        private ItemSlotTotem GhostTarget()
        {
            if (_interactor == null || ghostMaterial == null) return null;
            if (itemData == null || itemData.prefabPickable == null) return null;
            if (Time.time < _ghostHiddenUntil) return null;
            if (_playerState != null && _playerState.IsDead) return null;
            if (_interactor.HoveredInteractable is not ItemSlotTotem totem) return null;
            if (totem.IsOccupied || totem.PlacementPoint == null) return null;

            return totem;
        }

        private void HideGhost()
        {
            if (_ghost != null) _ghost.Hide();
        }

        public override void OnNetworkDespawn()
        {
            DestroyGhost();

            if (!IsOwner) return;

            inputReader.OnUseEvent -= InputReader_OnUseEvent;
            _playerInteractor = null;
            _interactor = null;
            _playerState = null;
        }

        public override void OnDestroy()
        {
            DestroyGhost();
            base.OnDestroy();
        }

        private void DestroyGhost()
        {
            if (_ghost == null) return;

            Destroy(_ghost.gameObject);
            _ghost = null;
        }
    }
}
