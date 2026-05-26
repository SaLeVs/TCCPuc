using Missions.PersonalMissions;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public abstract class MissionsManagerBase : NetworkBehaviour
    {
        [SerializeField] private MissionOwnershipSelector ownershipSelector;
        
        public MissionOwnershipSelector OwnershipSelector => ownershipSelector;
        public abstract bool IsComplete { get; protected set; }
    }
}

