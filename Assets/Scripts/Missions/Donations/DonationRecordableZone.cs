using System.Collections.Generic;
using Enums;
using Unity.Netcode;
using UnityEngine;

namespace Missions.Donations
{
    public class DonationRecordableZone : NetworkBehaviour
    {
        [SerializeField] private RecordableTarget targetType;

        private readonly HashSet<ulong> _watchingClients = new();

        private void Update()
        {
            if (!IsServer) return;
            if (_watchingClients.Count == 0) return;

            foreach (var clientId in _watchingClients)
            {
                DonationManager.Instance?.ReportRecordingProgress(clientId, targetType, Time.deltaTime);
            }
        }

        public void NotifyPlayerEnter(ulong clientId)
        {
            if (!IsServer) return;
            _watchingClients.Add(clientId);
        }

        public void NotifyPlayerExit(ulong clientId)
        {
            if (!IsServer) return;
            _watchingClients.Remove(clientId);
        }
    }
}