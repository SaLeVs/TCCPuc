using Components.Sound;
using Interfaces;
using Systems;
using Unity.Netcode;
using UnityEngine;

namespace Objects.PickupItems
{
    public class FloppyDiskPickable : NetworkBehaviour, IInteractable
    {
        [SerializeField] private SoundDefinitionSO pickupSound;

        public bool CanInteract(GameObject interactor) => true;

        public bool Interact(GameObject playerInteractor)
        {
            if (!playerInteractor.TryGetComponent(out NetworkObject netObj)) return false;

            if (!IsServer)
            {
                InteractServerRpc(netObj);
                return true;
            }

            TryGiveDisk(netObj);
            return true;
        }

        [Rpc(SendTo.Server)]
        private void InteractServerRpc(NetworkObjectReference playerRef)
        {
            if (!playerRef.TryGet(out NetworkObject playerNetObj)) return;
            TryGiveDisk(playerNetObj);
        }

        private void TryGiveDisk(NetworkObject playerNetObj)
        {
            if (!playerNetObj.TryGetComponent(out PlayerDiskHolder diskHolder)) return;

            diskHolder.GiveDisk();

            // No emitter: this object is despawned on the next line, before the sound reaches anyone.
            WorldSound.Play(pickupSound, transform.position);
            NetworkObject.Despawn();
        }
    }
}