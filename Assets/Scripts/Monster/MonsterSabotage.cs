using System.Collections.Generic;
using Monster.MonsterSabotages;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;


namespace Monster
{
    public class MonsterSabotage : NetworkBehaviour
    {
        [SerializeField] private List<ISabotageable> allSabotagesObjects;
        
        private List<ISabotageable> _currentSabotagesTargets;
        
        
        public ISabotageable GetAvailableTargets(SabotageType type)
        {
            foreach (ISabotageable target in _currentSabotagesTargets)
            {
                if (target.SabotageType == type && !target.IsSabotaged)
                {
                    return target;
                }
            }
            return null;
        }

        public void Execute(ISabotageable target)
        {
            target.Sabotage();
        }

        public void Restore(ISabotageable target)
        {
            target.Restore();
        }
    }
}