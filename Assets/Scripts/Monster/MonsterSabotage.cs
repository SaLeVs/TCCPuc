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
        [SerializeField] private float minTimeToSabotage = 10f;
        [SerializeField] private float maxTimeToSabotage = 30f;
        [SerializeField] private float minSabotageCooldown = 1f;
        [SerializeField] private float maxSabotageCooldown = 3f;
        [SerializeField] private List<GameObject> allSabotageObjects;
        
        public float MinTimeToSabotage => minTimeToSabotage;
        public float MaxTimeToSabotage => maxTimeToSabotage;
        public float MinSabotageCooldown => minSabotageCooldown;
        public float MaxSabotageCooldown => maxSabotageCooldown;
        
        
        private List<ISabotageable> _sabotageTargets;
        private SabotageType _currentSabotageType;
        
        
        public void Initialize()
        {
            _sabotageTargets = new List<ISabotageable>();
    
            foreach (GameObject obj in allSabotageObjects)
            {
                if (obj.TryGetComponent(out ISabotageable sabotageable))
                {
                    _sabotageTargets.Add(sabotageable);
                }
            }
        }

        public void ChooseSabotageType()
        {
            SabotageType[] allTypes = (SabotageType[])Enum.GetValues(typeof(SabotageType));
            _currentSabotageType = allTypes[Random.Range(0, allTypes.Length)];
        }
        
        public List<ISabotageable> GetAvailableTargets()
        {
            List<ISabotageable> available = new List<ISabotageable>();
    
            foreach (ISabotageable target in _sabotageTargets)
            {
                if (target.SabotageType == _currentSabotageType && !target.IsSabotaged)
                    available.Add(target);
            }
    
            return available;
        }
        
        public ISabotageable GetSabotagedTargets()
        {
            foreach (ISabotageable target in _sabotageTargets)
            {
                if (target.SabotageType == _currentSabotageType && target.IsSabotaged)
                {
                    return target;
                }
            }
            return null;
        }

        public void Execute(List<ISabotageable> targets)
        {
            foreach (ISabotageable target in targets)
            {
                int index = _sabotageTargets.IndexOf(target);
                if (index < 0) continue;
        
                target.Sabotage();
                SabotageClientRpc(index);
            }
        }

        public void Restore(ISabotageable target)
        {
            if (target == null) return;

            int index = _sabotageTargets.IndexOf(target);
            if (index < 0) return;
            
            target.Restore();
            RestoreClientRpc(index);
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void SabotageClientRpc(int index)
        {
            if (IsServer) return;
            
            _sabotageTargets[index].Sabotage();
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RestoreClientRpc(int index)
        {
            if (IsServer) return;
            
            _sabotageTargets[index].Restore();
        }
    }
}