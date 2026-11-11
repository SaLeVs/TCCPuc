using System;
using System.Collections.Generic;
using Components.Sound;
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

        [SerializeField] private SoundDefinitionSO sabotageSound;
        [SerializeField] private MonoBehaviour audienceProviderSource;
        [SerializeField] private float sabotageUnlockThreshold = 0.5f;
        [SerializeField] private float minSabotageCooldown = 15f;     
        [SerializeField] private float maxSabotageCooldown = 30f;
        [SerializeField] private float minSabotageStateDuration = 5f; 
        [SerializeField] private float maxSabotageStateDuration = 10f;
        [Tooltip("Sabotage targets placed by hand in the scene. Doors only: lamps go in the two lists below.")]
        [SerializeField] private List<GameObject> allSabotageObjects;

        [Header("Lights")]
        [Tooltip("Every regular lamp of the level. A blackout switches their point lights off. Fills itself in Play - lamps announce themselves as they load, the ones in rooms spawned mid-match included. 'Collect Scene Lights' in the context menu fills it ahead of time.")]
        [SerializeField] private List<LightSabotage> lights = new List<LightSabotage>();
        [Tooltip("Every emergency lamp of the level, the ones with Emergency ticked on their LightSabotage. Their point lights are off until a blackout and on during it; the mesh is always visible. Fills itself the same way.")]
        [SerializeField] private List<LightSabotage> emergencyLights = new List<LightSabotage>();

        public float MinSabotageCooldown => minSabotageCooldown;
        public float MaxSabotageCooldown => maxSabotageCooldown;
        public float MinSabotageStateDuration => minSabotageStateDuration;
        public float MaxSabotageStateDuration => maxSabotageStateDuration;
        public bool CanSabotage => _sabotageUnlocked;

        /// <summary>True while the regular lamps are out and the emergency ones are lit.</summary>
        public bool LightsOut => _lightsOut.Value;


        // Never null: ChatEventHub polls HasSabotagedOfType twice a second from the moment the
        // scene loads, which is long before the monster spawns and Initialize runs.
        private readonly List<ISabotageable> _sabotageTargets = new List<ISabotageable>();

        // Registered targets this monster must leave alone: the ones already in the list above,
        // which replicate by index and also announce themselves to SabotageRegistry.
        private readonly HashSet<ISabotageable> _registryExclusions = new HashSet<ISabotageable>();

        // The whole light sabotage is this one flag: every peer switches its own lamps off it,
        // regular ones out and emergency ones on, and back. A lamp that turns up later - a room
        // spawned mid-blackout, a player joining late - is simply brought up to date with it.
        private readonly NetworkVariable<bool> _lightsOut = new NetworkVariable<bool>();

        // What is already in the two lamp lists, so a lamp announcing itself twice is added once.
        private readonly HashSet<LightSabotage> _knownLights = new HashSet<LightSabotage>();

        private readonly List<SabotageType> _typesWithTargets = new List<SabotageType>();
        private SabotageType _currentSabotageType;
        private bool _sabotageUnlocked;
        private IAudienceProvider _audienceProvider;

        public void Initialize()
        {
            _audienceProvider = audienceProviderSource as IAudienceProvider;

            _sabotageTargets.Clear();
            _registryExclusions.Clear();

            // A lamp left in this list from before lamps had lists of their own is skipped here:
            // LightSabotage is not an ISabotageable any more.
            foreach (GameObject obj in allSabotageObjects)
            {
                if (obj != null && obj.TryGetComponent(out ISabotageable sabotageable))
                {
                    _sabotageTargets.Add(sabotageable);
                    _registryExclusions.Add(sabotageable);
                }
            }

            SabotageRegistry.OnRegistered -= SabotageRegistry_OnRegistered;
            SabotageRegistry.OnRegistered += SabotageRegistry_OnRegistered;

            CollectLights();

            LightRegistry.OnRegistered -= LightRegistry_OnRegistered;
            LightRegistry.OnRegistered += LightRegistry_OnRegistered;

            _lightsOut.OnValueChanged -= LightsOut_OnValueChanged;
            _lightsOut.OnValueChanged += LightsOut_OnValueChanged;

            // Already true for a client that joins in the middle of a blackout.
            ApplyLights(_lightsOut.Value);

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
        /// A room spawns mid-match, so its targets turn up after the sabotage they should already
        /// be part of. Every peer applies this for itself, off its own targets, so no RPC is needed:
        /// the sabotaged flags are already correct on all of them.
        /// </summary>
        private void SabotageRegistry_OnRegistered(ISabotageable target)
        {
            if (target == null || target.IsSabotaged) return;
            if (_registryExclusions.Contains(target)) return;

            if (HasSabotagedOfType(target.SabotageType)) target.Sabotage();
        }

        /// <summary>
        /// Rebuilds the two lamp lists: whatever was filled in the scene, sorted again by each
        /// lamp's own Emergency flag in case one sits in the wrong list, plus every lamp that is
        /// already live. Lamps disabled in the scene stay listed, so they follow the blackout too.
        /// </summary>
        private void CollectLights()
        {
            List<LightSabotage> listed = new List<LightSabotage>(lights.Count + emergencyLights.Count);
            listed.AddRange(lights);
            listed.AddRange(emergencyLights);

            lights.Clear();
            emergencyLights.Clear();
            _knownLights.Clear();

            foreach (LightSabotage lamp in listed) AddLight(lamp);
            foreach (LightSabotage lamp in LightRegistry.Lights) AddLight(lamp);
            foreach (LightSabotage lamp in LightRegistry.EmergencyLights) AddLight(lamp);
        }

        private void AddLight(LightSabotage lamp)
        {
            if (lamp == null || !_knownLights.Add(lamp)) return;

            (lamp.IsEmergency ? emergencyLights : lights).Add(lamp);
        }

        /// <summary>
        /// A lamp has just come alive - in practice, a room was spawned. It joins its list and is
        /// brought up to date, so a room that appears in the middle of a blackout arrives with its
        /// lamps out and its emergency lamps lit.
        /// </summary>
        private void LightRegistry_OnRegistered(LightSabotage lamp)
        {
            if (lamp == null) return;

            AddLight(lamp);
            lamp.SetBlackout(_lightsOut.Value);
        }

        private void LightsOut_OnValueChanged(bool previous, bool current)
        {
            ApplyLights(current);
        }

        /// <summary>
        /// The two lists are each other's opposite: in a blackout the point light of every regular
        /// lamp goes off and the one of every emergency lamp comes on, and the other way round when
        /// the power is back. Only the point lights - the meshes are never touched.
        /// </summary>
        private void ApplyLights(bool blackout)
        {
            SetBlackout(lights, blackout);
            SetBlackout(emergencyLights, blackout);
        }

        private void SetBlackout(List<LightSabotage> lamps, bool blackout)
        {
            for (int i = lamps.Count - 1; i >= 0; i--)
            {
                LightSabotage lamp = lamps[i];

                // Destroyed along with its room.
                if (lamp == null)
                {
                    lamps.RemoveAt(i);
                    continue;
                }

                lamp.SetBlackout(blackout);
            }
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
            // Lights go all at once, so there is something left to break only while they are on.
            if (type == SabotageType.Light) return !_lightsOut.Value && lights.Count > 0;

            foreach (ISabotageable target in _sabotageTargets)
            {
                if (target.SabotageType == type && !target.IsSabotaged) return true;
            }

            foreach (ISabotageable target in SabotageRegistry.All)
            {
                if (_registryExclusions.Contains(target)) continue;
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
                if (_registryExclusions.Contains(target)) continue;

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
                if (_registryExclusions.Contains(target)) continue;

                if (target.SabotageType == _currentSabotageType && target.IsSabotaged)
                {
                    return target;
                }
            }

            return null;
        }

        public void Execute(List<ISabotageable> targets)
        {
            // Lamps are not targets picked one by one: the monster takes all of them at once, and
            // the emergency lamps come on in their place.
            if (_currentSabotageType == SabotageType.Light)
            {
                if (!HasAvailableOfType(SabotageType.Light))
                {
                    Debug.LogWarning($"{name}: Execute called for Light with the lights already out, or no lamp in the level.", this);
                    return;
                }

                _lightsOut.Value = true;

                AnnounceSabotage();
                return;
            }

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

            AnnounceSabotage();
        }

        private void AnnounceSabotage()
        {
            // Server-side, like the rest of Execute: a local event here meant only the host heard it.
            WorldSound.Play(sabotageSound, transform.position, NetworkObject);
            OnSabotageStartedAnimation?.Invoke();
        }

        /// <summary>
        /// Puts every sabotaged target of a type back. Server-side — the electric circuit is what
        /// calls this for the lights. Returns how many were actually restored.
        /// </summary>
        public int RestoreAll(SabotageType type)
        {
            if (!IsServer) return 0;

            if (type == SabotageType.Light)
            {
                if (!_lightsOut.Value) return 0;

                _lightsOut.Value = false;
                return lights.Count;
            }

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
                if (_registryExclusions.Contains(target)) continue;
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
            if (type == SabotageType.Light) return _lightsOut.Value;

            foreach (ISabotageable target in _sabotageTargets)
            {
                if (target.SabotageType == type && target.IsSabotaged) return true;
            }

            foreach (ISabotageable target in SabotageRegistry.All)
            {
                if (_registryExclusions.Contains(target)) continue;
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
                if (_registryExclusions.Contains(target)) continue;
                if (target.SabotageType == type && !target.IsSabotaged) target.Sabotage();
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        private void RestoreRegisteredRpc(SabotageType type)
        {
            if (IsServer) return;

            foreach (ISabotageable target in SabotageRegistry.All)
            {
                if (_registryExclusions.Contains(target)) continue;
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

        /// <summary>Puts the sabotage clip back after something else borrowed the animator.</summary>
        public void ReplayAnimation()
        {
            OnSabotageStartedAnimation?.Invoke();
        }

        
        public void Uninitialize()
        {
            SabotageRegistry.OnRegistered -= SabotageRegistry_OnRegistered;
            LightRegistry.OnRegistered -= LightRegistry_OnRegistered;
            _lightsOut.OnValueChanged -= LightsOut_OnValueChanged;

            if (_audienceProvider != null)
                _audienceProvider.OnAudienceChanged -= AudienceManager_OnAudienceChanged;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Fills the two lamp lists from the open scene, so they can be read in the Inspector
        /// without entering Play. Optional: at runtime the lists complete and re-sort themselves.
        /// </summary>
        [ContextMenu("Collect Scene Lights")]
        private void CollectSceneLights()
        {
            UnityEditor.Undo.RecordObject(this, "Collect Scene Lights");

            lights.Clear();
            emergencyLights.Clear();

            foreach (LightSabotage lamp in FindObjectsByType<LightSabotage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (lamp.gameObject.scene != gameObject.scene) continue;

                (lamp.IsEmergency ? emergencyLights : lights).Add(lamp);
            }

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);

            Debug.Log($"{name}: {lights.Count} lamps and {emergencyLights.Count} emergency lamps listed.", this);
        }
#endif

    }
}