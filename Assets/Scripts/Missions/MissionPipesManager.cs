using System;
using Interfaces;
using Missions.PersonalMissions;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class MissionPipesManager : NetworkBehaviour, IMissionSpawnable
    {
        public event Action OnSpawnCompleted;
        
        [SerializeField] private MissionOwnershipSelector ownershipSelector;
        [SerializeField] private MissionCompleter missionCompleter;
        
        public bool IsComplete { get; private set; }
        
    
        public void RequestSpawn()
        {
            
        }
    }
}

