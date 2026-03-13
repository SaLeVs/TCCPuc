using Inputs;
using Interfaces;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerInteractor : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private InputReader inputReader;
        [SerializeField] private Transform playerView;

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
            Ray ray = new Ray(playerView.position, playerView.forward);
            Debug.DrawRay(playerView.position, playerView.forward * interactDistance, Color.red, 2f);
            
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

