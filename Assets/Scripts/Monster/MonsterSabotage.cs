using System;
using System.Collections.Generic;
using System.Linq;
using Monster.MonsterSabotages;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;


namespace Monster
{
    public class MonsterSabotage : NetworkBehaviour
    {
        [SerializeField] private List<MonoBehaviour> allSabotageObjects;
        
        private List<ISabotageable> _sabotageTargets;
        private SabotageType _currentSabotageType;
        
        
        public void Initialize()
        {
            _sabotageTargets = allSabotageObjects.OfType<ISabotageable>().ToList();
        }

        public void ChooseSabotageType()
        {
            SabotageType[] allTypes = (SabotageType[])Enum.GetValues(typeof(SabotageType));
            _currentSabotageType = allTypes[Random.Range(0, allTypes.Length)];
        }

        public ISabotageable GetAvailableTarget()
        {
            foreach (ISabotageable target in _sabotageTargets)
            {
                if (target.SabotageType == _currentSabotageType && !target.IsSabotaged)
                    return target;
            }
            return null;
        }
        
        public List<ISabotageable> GetSabotagedTargets()
        {
            return _sabotageTargets.Where(sabotageObject => sabotageObject.IsSabotaged).ToList();
        }

        public void Execute(ISabotageable target)
        {
            if (target == null) return;
            target.Sabotage();
        }

        public void Restore(ISabotageable target)
        {
            if (target == null) return;
            target.Restore();
        }
    }
}