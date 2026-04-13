using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Monster
{
    public class MonsterChase : NetworkBehaviour
    {
        public List<Transform> _monsterTargets;
        
        public void Initialize(List<Transform> monsterTargets)
        {
            _monsterTargets = monsterTargets;
        }
        
        public void ChaseUpdate()
        {
            
        }
    }

}


