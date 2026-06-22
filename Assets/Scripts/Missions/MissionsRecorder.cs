using System;
using System.Collections.Generic;
using Enums;
using UnityEngine;

namespace Missions
{
    [Serializable]
    public struct RecordableTargetConfig
    {
        public RecordableTarget targetType;
        public float maxDistanceToOtherTargets;
    }

    public class MissionsRecorder : MainMission
    {
        [SerializeField] private RecordableTargetConfig[] requiredTargets;
        [SerializeField] private float checkInterval = 0.5f;
        

        private Dictionary<RecordableTarget, HashSet<ulong>> _playersWatching = new();
        private Dictionary<RecordableTarget, GameObject> _targetObjects = new();
        private HashSet<RecordableTarget> _recordedTargets = new();
        
        private MissionManager _missionManager;
        private float _checkTimer;
        

        public override void StartMission()
        {
            if (!IsServer) return;

            foreach (RecordableTargetConfig config in requiredTargets)
            {
                _playersWatching[config.targetType] = new HashSet<ulong>();
            }
            
            _missionManager = FindFirstObjectByType<MissionManager>();
        }

        public override void AbortMission()
        {
            if (!IsServer) return;

            _playersWatching.Clear();
            _targetObjects.Clear();
            _recordedTargets.Clear();
        }
        

        public void ReportTargetEnter(ulong clientId, GameObject target, RecordableTarget targetType)
        {
            if (!IsServer) return;
            if (!_playersWatching.ContainsKey(targetType)) return;

            _targetObjects[targetType] = target;
            _playersWatching[targetType].Add(clientId);
            
            CheckMissionComplete();
        }

        public void ReportTargetExit(ulong clientId, RecordableTarget targetType)
        {
            if (!IsServer) return;
            if (!_playersWatching.ContainsKey(targetType)) return;

            _playersWatching[targetType].Remove(clientId);

            if (_playersWatching[targetType].Count == 0)
            {
                _targetObjects.Remove(targetType);
            }
            
        }

        private void Update()
        {
            if (!IsServer) return;
            if (_recordedTargets.Count >= requiredTargets.Length) return;

            _checkTimer += Time.deltaTime;
            if (_checkTimer >= checkInterval)
            {
                _checkTimer = 0f;
                CheckMissionComplete();
            }
        }
        
        private void CheckMissionComplete()
        {
            if (_recordedTargets.Count >= requiredTargets.Length) return;

            foreach (RecordableTargetConfig config in requiredTargets)
            {
                if (_playersWatching[config.targetType].Count == 0) return;
                if (!_targetObjects.TryGetValue(config.targetType, out GameObject thisObj)) return;

                foreach (RecordableTargetConfig other in requiredTargets)
                {
                    if (other.targetType == config.targetType) continue;
                    if (!_targetObjects.TryGetValue(other.targetType, out GameObject otherObj)) return;

                    float dist = Vector3.Distance(thisObj.transform.position, otherObj.transform.position);

                    if (dist > config.maxDistanceToOtherTargets)
                    {
                        return;
                    }
                }

                _recordedTargets.Add(config.targetType);
            }

            if (_recordedTargets.Count >= requiredTargets.Length)
            {
                _missionManager.CompleteMainMission();
            }
        }
        
    }
}