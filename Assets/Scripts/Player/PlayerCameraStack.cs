using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Player
{
    public class PlayerCameraStack : NetworkBehaviour
    {
        [SerializeField] private Camera playerCamera;

        private Camera _mainCamera;
        private UniversalAdditionalCameraData _mainCameraData;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                if (playerCamera != null)
                    playerCamera.enabled = false;

                return;
            }

            StartCoroutine(AttachCameraToStack());
        }

        private IEnumerator AttachCameraToStack()
        {
            while (Camera.main == null)
                yield return null;

            _mainCamera = Camera.main;

            if (_mainCamera == null)
                yield break;

            _mainCameraData = _mainCamera.GetUniversalAdditionalCameraData();

            if (_mainCameraData == null)
            {
                Debug.LogWarning("Main Camera não tem UniversalAdditionalCameraData.");
                yield break;
            }

            var playerCameraData = playerCamera.GetUniversalAdditionalCameraData();
            if (playerCameraData != null)
            {
                playerCameraData.renderType = CameraRenderType.Overlay;
            }

            if (!_mainCameraData.cameraStack.Contains(playerCamera))
            {
                _mainCameraData.cameraStack.Add(playerCamera);
                _mainCameraData.renderPostProcessing = false;
            }

            playerCamera.enabled = true;
        }

        public override void OnNetworkDespawn()
        {
            if (_mainCameraData != null && playerCamera != null)
            {
                _mainCameraData.cameraStack.Remove(playerCamera);
            }
        }
    }
}