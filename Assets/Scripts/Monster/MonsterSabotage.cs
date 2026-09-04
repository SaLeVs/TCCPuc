using System;
using System.Collections.Generic;
using Interfaces;
using Monster.MonsterSabotages;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;


namespace Monster
{
    public class MonsterSabotage : NetworkBehaviour
    {
        public event Action OnSabotageStartedAnimation;
        public event Action OnSabotageEndedAnimation;
        public static Action<Vector3> OnSabotageSound;
        
        [SerializeField] private MonoBehaviour audienceProviderSource; 
        [SerializeField] private float sabotageUnlockThreshold = 0.5f;
        [SerializeField] private float minSabotageCooldown = 15f;     
        [SerializeField] private float maxSabotageCooldown = 30f;
        [SerializeField] private float minSabotageStateDuration = 5f; 
        [SerializeField] private float maxSabotageStateDuration = 10f;
        [SerializeField] private List<GameObject> allSabotageObjects;
        
        public float MinSabotageCooldown => minSabotageCooldown;
        public float MaxSabotageCooldown => maxSabotageCooldown;
        public float MinSabotageStateDuration => minSabotageStateDuration;
        public float MaxSabotageStateDuration => maxSabotageStateDuration;
        public bool CanSabotage => _sabotageUnlocked;
        
        
        private List<ISabotageable> _sabotageTargets;
        private SabotageType _currentSabotageType;
        private bool _sabotageUnlocked;
        private IAudienceProvider _audienceProvider;
        
        public void Initialize()
        {
            _audienceProvider = audienceProviderSource as IAudienceProvider;

            _sabotageTargets = new List<ISabotageable>();
            
            foreach (GameObject obj in allSabotageObjects)
            {
                if (obj.TryGetComponent(out ISabotageable sabotageable))
                    _sabotageTargets.Add(sabotageable);
            }

            _sabotageUnlocked = _audienceProvider.NormalizedAudience > sabotageUnlockThreshold;

            _audienceProvider.OnAudienceChanged += AudienceManager_OnAudienceChanged;;
        }


        private void AudienceManager_OnAudienceChanged(float audience)
        {
            _sabotageUnlocked = _audienceProvider.NormalizedAudience > sabotageUnlockThreshold;
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
                {
                    available.Add(target);
                }
            }
            
            foreach (ISabotageable target in SabotageRegistry.All)
            {
                if (target.SabotageType == _currentSabotageType && !target.IsSabotaged)
                {
                    available.Add(target);
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

            foreach (ISabotageable target in SabotageRegistry.All)
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
            bool hitRegistered = false;

            foreach (ISabotageable target in targets)
            {
                target.Sabotage();

                int index = _sabotageTargets.IndexOf(target);

                if (index < 0)
                {
                    hitRegistered = true;
                    continue;
                }

                SabotageClientRpc(index);
            }

            // Registered targets have no index that means the same thing on every peer, so they
            // replicate by type instead: everyone spawns the same rooms, so the sets match.
            if (hitRegistered)
            {
                SabotageRegisteredRpc(_currentSabotageType);
            }

            OnSabotageSound?.Invoke(transform.position);
            OnSabotageStartedAnimation?.Invoke();
        }

        /// <summary>
        /// Puts every sabotaged target of a type back. Server-side — the electric circuit is what
        /// calls this for the lights. Returns how many were actually restored.
        /// </summary>
        public int RestoreAll(SabotageType type)
        {
            if (!IsServer) return 0;

            int restored = 0;

            for (int i = 0; i < _sabotageTargets.Count; i++)
            {
                ISabotageable target = _sabotageTargets[i];
                if (target.SabotageType != type || !target.IsSabotaged) continue;

                target.Restore();
                RestoreClientRpc(i);
                restored++;
            }

            bool hitRegistered = false;

            foreach (ISabotageable target in SabotageRegistry.All)
            {
                if (target.SabotageType != type || !target.IsSabotaged) continue;

                target.Restore();
                hitRegistered = true;
                restored++;
            }

            if (hitRegistered)
            {
                RestoreRegisteredRpc(type);
            }

            return restored;
        }

        /// <summary>Is anything of this type currently broken? Gates the circuit's interaction.</summary>
        public bool HasSabotagedOfType(SabotageType type)
        {
            foreach (ISabotageable target in _sabotageTargets)
            {
                if (target.SabotageType == type && target.IsSabotaged) return true;
            }

            foreach (ISabotageable target in SabotageRegistry.All)
            {
                if (target.SabotageType == type && target.IsSabotaged) return true;
            }

            return false;
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void SabotageRegisteredRpc(SabotageType type)
        {
            if (IsServer) return;

            foreach (ISabotageable target in SabotageRegistry.All)
            {
                if (target.SabotageType == type && !target.IsSabotaged) target.Sabotage();
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RestoreRegisteredRpc(SabotageType type)
        {
            if (IsServer) return;

            foreach (ISabotageable target in SabotageRegistry.All)
            {
                if (target.SabotageType == type && target.IsSabotaged) target.Restore();
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

        public void EndSabotage()
        {
            OnSabotageEndedAnimation?.Invoke();
        }

        
        public void Uninitialize()
        {
            if (_audienceProvider != null)
                _audienceProvider.OnAudienceChanged -= AudienceManager_OnAudienceChanged;
        }
        
    }
}