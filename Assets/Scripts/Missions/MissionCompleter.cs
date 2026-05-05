using System;
using ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class MissionCompleter : NetworkBehaviour
    {
        public static event Action<MissionSO> OnMissionCompleted;

        [SerializeField] private MissionSO mission;
    
        public MissionSO Mission => mission;
    
        public void Complete()
        {
            if (!IsServer) return;

            OnMissionCompleted?.Invoke(mission);
        }
    } 
}

