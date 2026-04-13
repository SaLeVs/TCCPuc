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
        [SerializeField] private float minSabotageCooldown = 15f;     
        [SerializeField] private float maxSabotageCooldown = 30f;
        [SerializeField] private float minSabotageStateDuration = 5f; 
        [SerializeField] private float maxSabotageStateDuration = 10f;
        [SerializeField] private List<GameObject> allSabotageObjects;
        
        public float MinSabotageCooldown => minSabotageCooldown;
        public float MaxSabotageCooldown => maxSabotageCooldown;
        public float MinSabotageStateDuration => minSabotageStateDuration;
        public float MaxSabotageStateDuration => maxSabotageStateDuration;
        
        
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
            Debug.Log($"ChooseSabotageType: {_currentSabotageType}");
        }
        
        public List<ISabotageable> GetAvailableTargets()
        {
            List<ISabotageable> available = new List<ISabotageable>();
    
            foreach (ISabotageable target in _sabotageTargets)
            {
                if (target.SabotageType == _currentSabotageType && !target.IsSabotaged)
                {
                    available.Add(target);
                    Debug.Log($"Add Sabotage target {target} to available list");
                }
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
                Debug.Log($"Sabotage target in server");
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
            Debug.Log($"Sabotage target in client");
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RestoreClientRpc(int index)
        {
            if (IsServer) return;
            
            _sabotageTargets[index].Restore();
        }
    }
}