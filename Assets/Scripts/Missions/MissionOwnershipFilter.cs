using Missions.PersonalMissions;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class MissionOwnershipFilter : NetworkBehaviour
    {
        private readonly NetworkVariable<NetworkObjectReference> _managerRef = new();

        private MissionsManagerBase Manager
        {
            get
            {
                if (_managerRef.Value.TryGet(out NetworkObject networkObject))
                {
                    if (networkObject.TryGetComponent(out MissionsManagerBase missionManager))
                    {
                        return missionManager;
                    }
                }
                
                return null;
            }
        }

        public override void OnNetworkSpawn()
        {
            _managerRef.OnValueChanged += OnManagerChanged;
        }

        private void OnManagerChanged(NetworkObjectReference previousValue, NetworkObjectReference nextValue)
        {
            Debug.Log("MissionOwnershipFilter: Manager changed");
        }

        public void SetManager(MissionsManagerBase manager)
        {
            if (!IsServer || manager == null || manager.NetworkObject == null) return;
            _managerRef.Value = manager.NetworkObject;
        }

        public bool CanClientInteract(ulong clientId)
        {
            MissionsManagerBase manager = Manager;

            Debug.Log($"CanClientInteract({clientId})");
            Debug.Log($"Manager = {manager}");

            if (manager == null) return false;

            MissionOwnershipSelector selector = manager.OwnershipSelector;

            Debug.Log($"Selector = {selector}");

            if (selector == null) return false;

            Debug.Log($"Owner Assigned = {selector.IsMissionOwnerAssigned}");
            Debug.Log($"Owner Client = {selector.OwnerClientIdSelector}");
            Debug.Log(
                $"Reading selector {selector.NetworkObjectId}"
            );
            return selector.IsMissionOwner(clientId);
        }

        public override void OnNetworkDespawn()
        {
            _managerRef.OnValueChanged -= OnManagerChanged;
        }
    }
}