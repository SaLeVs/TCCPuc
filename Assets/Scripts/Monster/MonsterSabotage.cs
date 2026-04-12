using System.Collections.Generic;
using Monster.MonsterSabotages;
using Unity.Netcode;
using UnityEngine;


namespace Monster
{
    public class MonsterSabotage : NetworkBehaviour
    {
        [SerializeField] private List<SabotageTarget> sabotageTargets;
        
        private Dictionary<SabotageType, Sabotage> _sabotages;
        private MonsterBrain _monsterBrain;
        
        
        public void Initialize(MonsterBrain monsterBrain)
        {
            _monsterBrain = monsterBrain;
            
            _sabotages = new Dictionary<SabotageType, Sabotage>
            {
                {
                    SabotageType.Light, new LightSabotage()
                },
            };
            
        }

        public SabotageTarget GetAvailableTarget(SabotageType type)
        {
            foreach (SabotageTarget target in sabotageTargets)
            {
                if (target.SabotageType == type && !target.IsSabotaged)
                    return target;
            }
            return null;
        }

        public void Execute(SabotageTarget target)
        {
            if (_sabotages.TryGetValue(target.SabotageType, out Sabotage sabotage))
            {
                sabotage.Execute(target, _monsterBrain);
            }
        }
        
    }
}