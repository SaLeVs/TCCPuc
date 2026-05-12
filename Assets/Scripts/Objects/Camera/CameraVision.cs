using Components;
using Enums;
using Missions;
using Unity.Netcode;
using UnityEngine;

namespace Objects.Camera
{
    public class CameraVision : NetworkBehaviour
    {
        [SerializeField] private VisionSensor visionSensor;

        
        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                visionSensor.OnTargetEnterServer += VisionSensor_OnTargetEnterServer;
                visionSensor.OnTargetExitServer += VisionSensor_OnTargetExitServer;
            }
        }

        private void VisionSensor_OnTargetEnterServer(GameObject target, RecordableTarget targetType)
        {
            MissionsRecorder recorder = FindAnyObjectByType<MissionsRecorder>();
            if (recorder == null) return;

            float distance = Vector3.Distance(transform.position, target.transform.position);
            recorder.ReportTargetEnter(OwnerClientId, targetType, distance);
        }

        private void VisionSensor_OnTargetExitServer(GameObject target, RecordableTarget targetType)
        {
            MissionsRecorder recorder = FindAnyObjectByType<MissionsRecorder>();
            if (recorder == null) return;

            recorder.ReportTargetExit(OwnerClientId, targetType);
        }


        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                visionSensor.OnTargetEnterServer -= VisionSensor_OnTargetEnterServer;
                visionSensor.OnTargetExitServer -= VisionSensor_OnTargetExitServer;
            }
        }
    }
}

