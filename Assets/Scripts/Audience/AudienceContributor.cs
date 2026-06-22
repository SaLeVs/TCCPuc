using System.Collections.Generic;
using Components;
using Enums;
using Unity.Netcode;
using UnityEngine;

namespace Audience
{
    [RequireComponent(typeof(VisionSensor))]
    public class AudienceContributor : NetworkBehaviour
    {
        [SerializeField] private VisionSensor visionSensor;
        
        public bool IsWatchingSomething => _activeObjects.Count > 0;
        public bool HasBeenSeen(GameObject target) => _seenObjects.ContainsKey(target.GetInstanceID());
        public int UniqueObjectsSeen => _seenObjects.Count;
        
        private Dictionary<GameObject, float> _activeObjects = new Dictionary<GameObject, float>();
        private Dictionary<int, float> _pendingViewTime = new Dictionary<int, float>();
        
        private Dictionary<int, float> _seenObjects = new Dictionary<int, float>();
        private HashSet<GameObject> _presenceObjects = new HashSet<GameObject>();
        
        
        public override void OnNetworkSpawn()
        {
            if (!IsOwner) return;

            visionSensor.OnTargetEnter += HandleTargetEnterStatic;
            visionSensor.OnTargetExit += HandleTargetExitStatic;

            visionSensor.OnTargetEnterStatic += HandleTargetEnterStatic;
            visionSensor.OnTargetExitStatic += HandleTargetExitStatic;
        }
        
        
        private void Update()
        {
            if (!IsOwner) return;

            TickActiveObjects(Time.deltaTime);
            TickPresenceObjects();
        }
        
        private void HandleTargetEnterStatic(GameObject target)
        {
            if (target.TryGetComponent(out RecordableIdentifier id))
            {
                HandleTargetEnter(target, id.targetType);
            }
        }
        
        private void HandleTargetExitStatic(GameObject target)
        {
            if (target.TryGetComponent(out RecordableIdentifier id))
            {
                HandleTargetExit(target, id.targetType);
            }
        }
        
        private void HandleTargetEnter(GameObject target, RecordableTarget targetType)
        {
            if (!target.TryGetComponent(out RecordableIdentifier identifier)) return;

            int id = target.GetInstanceID();

            if (_seenObjects.TryGetValue(id, out float seenAt))
            {
                if (identifier.canBeReviewed && Time.time - seenAt >= identifier.reviewCooldown)
                {
                    _seenObjects.Remove(id);
                    _activeObjects[target] = 0f;
                }
                else
                {
                    _presenceObjects.Add(target);
                }
                return;
            }

            if (!_activeObjects.ContainsKey(target))
            {
                float resumedTime = 0f;
                if (_pendingViewTime.TryGetValue(id, out float saved))
                {
                    resumedTime = saved;
                    _pendingViewTime.Remove(id);
                }
                else
                {
                    Debug.Log($"AudienceContributor: {target.name} entered for the first time.");
                }

                _activeObjects[target] = resumedTime;
            }
        }

        private void HandleTargetExit(GameObject target, RecordableTarget targetType)
        {
            if (_presenceObjects.Contains(target))
            {
                _presenceObjects.Remove(target);
                return;
            }

            if (!_activeObjects.ContainsKey(target)) return;
            if (!target.TryGetComponent(out RecordableIdentifier identifier)) return;

            float accumulatedTime = _activeObjects[target];
            int id = target.GetInstanceID();

            if (accumulatedTime < identifier.minimumViewTime)
            {
                _pendingViewTime[id] = accumulatedTime;
                _activeObjects.Remove(target);
            }
            else
            {
                _activeObjects.Remove(target);
            }
        }
        
        private void TickActiveObjects(float deltaTime)
        {
            List<GameObject> keys = new List<GameObject>(_activeObjects.Keys);

            foreach (GameObject target in keys)
            {
                if (target == null)
                {
                    _activeObjects.Remove(target);
                    continue;
                }

                if (!target.TryGetComponent(out RecordableIdentifier identifier)) continue;

                _activeObjects[target] += deltaTime;

                if (_activeObjects[target] < identifier.minimumViewTime) continue;

                SubmitAudienceServerRpc(identifier.audienceGain);

                _seenObjects[target.GetInstanceID()] = Time.time;
                _activeObjects.Remove(target);
            }
        }

        [Rpc(SendTo.Server)]
        private void SubmitAudienceServerRpc(float gain)
        {
            AudienceManager.Instance.SubmitGain(gain);
        }
        
        private void TickPresenceObjects()
        {
            if (_presenceObjects.Count == 0)
                return;

            SubmitPresenceServerRpc();
        }

        [Rpc(SendTo.Server)]
        private void SubmitPresenceServerRpc()
        {
            AudienceManager.Instance.SubmitPresence();
        }
        
        [Rpc(SendTo.ClientsAndHost)]
        public void ResetSeenObjectsServerRpc()
        {
            _seenObjects.Clear();
            _activeObjects.Clear();
            _presenceObjects.Clear();
            _pendingViewTime.Clear();
        }
        
        
        public override void OnNetworkDespawn()
        {
            if (!IsOwner) return;

            visionSensor.OnTargetEnter -= HandleTargetEnterStatic;
            visionSensor.OnTargetExit -= HandleTargetExitStatic;

            visionSensor.OnTargetEnterStatic -= HandleTargetEnterStatic;
            visionSensor.OnTargetExitStatic -= HandleTargetExitStatic;
        }
        
    }
}