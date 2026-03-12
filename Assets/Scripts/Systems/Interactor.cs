using Inputs;
using Unity.Netcode;
using UnityEngine;

namespace Systems
{
    public class Interactor : NetworkBehaviour
    {
        [SerializeField] private InputReader inputReader;

        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                inputReader.OnInteractEvent += InputReader_OnInteractEvent;
            }
        }

        
        private void InputReader_OnInteractEvent()
        {
            Debug.Log("Interact");
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

