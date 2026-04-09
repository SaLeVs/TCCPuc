using Components;
using Unity.Netcode;
using UnityEngine;

namespace Objects.Camera
{
    public class CameraVision : NetworkBehaviour
    {
        [SerializeField] private VisionSensor visionSensor;

        
        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                visionSensor.OnTargetEnter += VisionSensor_OnTargetEnter;
                visionSensor.OnTargetExit += VisionSensor_OnTargetExit;
            }
        }

        private void VisionSensor_OnTargetEnter(GameObject objectVision)
        {
            Debug.Log($"Object entered in vision sensor: {objectVision.name}");
        }
        
        private void VisionSensor_OnTargetExit(GameObject objectVision)
        {
            Debug.Log($"Object exit in vision sensor: {objectVision.name}");
        }


        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                visionSensor.OnTargetEnter -= VisionSensor_OnTargetEnter;
                visionSensor.OnTargetExit -= VisionSensor_OnTargetExit;
            }
        }
    }
}

