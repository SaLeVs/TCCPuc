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

            if (AllAlivePlayersEscaped())
            {
                BroadcastVictoryRpc();
            }
            else
            {
                playerState.WinRpc();
            }
        }

        private bool AllAlivePlayersEscaped()
        {
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
            {
                if (_escapedPlayers.Contains(clientId)) continue;

                NetworkObject playerObj = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);

                if (playerObj == null) continue;
                if (playerObj.TryGetComponent(out PlayerState ps) && ps.IsDead) continue;

                return false;
            }
            return true;
        }
        
        [Rpc(SendTo.ClientsAndHost)]
        private void BroadcastVictoryRpc()
        {
            NetworkObject localPlayerObj = NetworkManager.Singleton.LocalClient?.PlayerObject;

            if (localPlayerObj != null && localPlayerObj.TryGetComponent(out PlayerState playerStateReference))
            {
                playerStateReference.TriggerVictoryLocally();
            }
        }
        
    }
}