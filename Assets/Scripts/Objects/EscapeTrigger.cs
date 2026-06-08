using Player;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Objects
{
    public class EscapeTrigger : NetworkBehaviour
    {
        private readonly HashSet<ulong> _escapedPlayers = new();

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            if (!other.TryGetComponent(out NetworkObject netObj)) return;
            if (!netObj.TryGetComponent(out PlayerState playerState)) return;
            if (playerState.IsDead) return;
            if (!_escapedPlayers.Add(netObj.OwnerClientId)) return;

            playerState.HidePlayerRpc();
            playerState.WinRpc();  
        }
    }
}