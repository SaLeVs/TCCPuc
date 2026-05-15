using System;
using Interfaces;
using Unity.Netcode;

namespace Missions
{
    public class MissionPipesManager : NetworkBehaviour, IMissionSpawnable
    {
        public event Action OnSpawnCompleted;
    
        public void RequestSpawn()
        {
            
        }
    }
}

