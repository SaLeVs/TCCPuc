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
        private readonly List<SabotageType> _typesWithTargets = new List<SabotageType>();
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

            // The cast returns null for an empty field or a component that does not implement
            // the interface, and the next line used to dereference it — a NullReferenceException
            // here aborts the rest of the monster's OnNetworkSpawn, so the whole AI comes up
            // half-initialised over one unassigned inspector slot.
            if (_audienceProvider == null)
            {
                Debug.LogError(
                    $"{name}: audienceProviderSource is empty or does not implement IAudienceProvider. " +
                    "Sabotage will stay locked for the whole match.", this);
                return;
            }

            _sabotageUnlocked = _audienceProvider.NormalizedAudience > sabotageUnlockThreshold;

            _audienceProvider.OnAudienceChanged += AudienceManager_OnAudienceChanged;
        }


        private void AudienceManager_OnAudienceChanged(float audience)
        {
            _sabotageUnlocked = _audienceProvider.NormalizedAudience > sabotageUnlockThreshold;
        }
        
        /// <summary>
        /// Picks a sabotage type that actually has something left to break, and reports whether
        /// one exists at all.
        ///
        /// <para>It used to draw from every type blindly. With only Light and Door in the enum,
        /// once the lights were all out that was a coin flip on doing nothing — the monster would
        /// still stand still for the full sabotage duration playing the animation over an empty
        /// target list. Choosing from the types that have targets removes the wasted half.</para>
        /// </summary>
        public bool TryChooseSabotageType()
        {
            _typesWithTargets.Clear();

            foreach (SabotageType type in (SabotageType[])Enum.GetValues(typeof(SabotageType)))
            {
                if (HasAvailableOfType(type)) _typesWithTargets.Add(type);
            }

            if (_typesWithTargets.Count == 0) return false;

            _currentSabotageType = _typesWithTargets[Random.Range(0, _typesWithTargets.Count)];
            return true;
        }

        /// <summary>Is there anything of this type still intact? Mirror of <see cref="HasSabotagedOfType"/>.</summary>
        public bool HasAvailableOfType(SabotageType type)
        {
            foreach (ISabotageable target in _sabotageTargets)
            {
                if (target.SabotageType == type && !target.IsSabotaged) return true;
            }

            foreach (ISabotageable target in SabotageRegistry.All)
            {
                if (target.SabotageType == type && !target.IsSabotaged) return true;
            }

            return false;
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
            // Belt and braces: MonsterRoaming already refuses to enter the state without a
            // target. If we somehow get here empty, stay silent rather than announcing a
            // sabotage that breaks nothing.
            if (targets == null || targets.Count == 0)
            {
                Debug.LogWarning($"{name}: Execute called with no targets for {_currentSabotageType}.", this);
                return;
            }

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