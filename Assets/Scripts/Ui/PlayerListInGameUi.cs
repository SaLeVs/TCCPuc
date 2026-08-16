using Inputs;
using Network;
using Unity.Netcode;
using UnityEngine;

namespace UI
{
    public class PlayerListInGameUi : NetworkBehaviour
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private GameObject playerListUi;
        [SerializeField] private GameObject playerUiContainer;
        [SerializeField] private RectTransform playerListContent;
        
        private bool _isPlayerListOpen;
        
        
        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;
            
            inputReader.OnPlayerListEvent += InputReader_OnPlayerListPressed;
            VivoxManager.instance += SpawnManager_OnPlayerSpawned;
        }

        private void InputReader_OnPlayerListPressed()
        {
            _isPlayerListOpen = !_isPlayerListOpen;

            if (playerListUi)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            
            playerListUi.SetActive(_isPlayerListOpen);
        }
        
        
        private void SpawnManager_OnPlayerSpawned()
        {
            
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;
            
            inputReader.OnPlayerListEvent -= InputReader_OnPlayerListPressed;
        }
        
    }
}
