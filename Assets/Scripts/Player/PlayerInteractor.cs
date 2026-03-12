using Inputs;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerInteractor : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Camera playerCamera;

        [Header("Settings")]
        [SerializeField] private float interactDistance = 3f;
        
        
        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnInteractEvent += InputReader_OnInteractEvent;
            }
        }

        
        private void InputReader_OnInteractEvent()
        {
            Interact();
        }

        private void Interact()
        {
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
            {
                if (hit.collider.TryGetComponent<IInteractable>(out IInteractable interactable))
                {
                    interactable.Interact(gameObject);
                }
            }
        }
        
        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                inputReader.OnInteractEvent -= InputReader_OnInteractEvent;
            }
        }
    }
}

