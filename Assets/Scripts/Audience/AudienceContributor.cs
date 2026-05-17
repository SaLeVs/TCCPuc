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
        [SerializeField] private float diminishFactor = 0.4f;
        [SerializeField, Range(0f, 1f)] private float minimumGainMultiplier = 0.05f;
        
        public bool IsWatchingSomething => _activeObjects.Count > 0;
        public bool HasBeenSeen(GameObject target) => _seenObjects.Contains(target.GetInstanceID());
        public int UniqueObjectsSeen => _seenObjects.Count;
        
        private Dictionary<GameObject, float> _activeObjects = new Dictionary<GameObject, float>();
        private Dictionary<int, float> _pendingViewTime = new Dictionary<int, float>();
        
        private HashSet<int> _seenObjects = new HashSet<int>();
        private HashSet<GameObject> _presenceObjects = new HashSet<GameObject>();


        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                visionSensor.OnTargetEnterServer += HandleTargetEnter;
                visionSensor.OnTargetExitServer  += HandleTargetExit;
            }
        }
        

        private void Update()
        {
            if (IsServer)
            {
                TickActiveObjects(Time.deltaTime);
                TickPresenceObjects();
            }
        }
        
        private void HandleTargetEnter(GameObject target, RecordableTarget targetType)
        {
            if (!target.TryGetComponent(out RecordableIdentifier identifier)) return;
            
            int id = target.GetInstanceID();
            
            if (_seenObjects.Contains(id))
            {
                if (identifier.canBeReviewedForDecay)
                {
                    _presenceObjects.Add(target);
                    Debug.Log($"AudienceContributor: {target.name} re-entered view for decay prevention.");
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
                    Debug.Log($"AudienceContributor: {target.name} re-entered, resuming at {resumedTime}s.");
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
                Debug.Log($"AudienceContributor: {target.name} left presence view.");
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
                Debug.Log($"AudienceContributor: {target.name} left early ({accumulatedTime}s / {identifier.minimumViewTime}s). Progress saved.");
            }
            else
            {
                _activeObjects.Remove(target);
                Debug.Log($"AudienceContributor: {target.name} confirmed seen on exit.");
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

                _activeObjects[target] += deltaTime;
                float secondsInView = _activeObjects[target];

                float baseGain = 0f;

                if (target.TryGetComponent(out RecordableIdentifier identifier))
                {
                    baseGain = identifier.audienceGainPerSecond;
                }

                float multiplier = Mathf.Max(minimumGainMultiplier, 1f / (1f + diminishFactor * secondsInView));

                AudienceManager.Instance.SubmitGain(baseGain * multiplier);
            }
        }
        
        private void TickPresenceObjects()
        {
            if (_presenceObjects.Count == 0) return;
            
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
            if (IsServer)
            {
                visionSensor.OnTargetEnterServer -= HandleTargetEnter;
                visionSensor.OnTargetExitServer  -= HandleTargetExit;
            }
        }
        
    }
}