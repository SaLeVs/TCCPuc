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

        private Dictionary<RecordableTarget, HashSet<ulong>> _playersWatching = new();
        private Dictionary<RecordableTarget, GameObject> _targetObjects = new();
        private HashSet<RecordableTarget> _recordedTargets = new();
        
        private MissionManager _missionManager;
        

        public override void StartMission()
        {
            if (!IsServer) return;

            foreach (RecordableTargetConfig config in requiredTargets)
            {
                _playersWatching[config.targetType] = new HashSet<ulong>();
            }
            
            _missionManager = FindFirstObjectByType<MissionManager>();
            
            Debug.Log("MissionsRecorder: Mission started!");
        }

        public override void AbortMission()
        {
            if (!IsServer) return;

            _playersWatching.Clear();
            _targetObjects.Clear();
            _recordedTargets.Clear();
            Debug.Log("MissionsRecorder: Mission aborted!");
        }

        public void ReportTargetEnter(ulong clientId, GameObject target, RecordableTarget targetType)
        {
            if (!IsServer) return;
            if (!_playersWatching.ContainsKey(targetType)) return;

            _targetObjects[targetType] = target;
            _playersWatching[targetType].Add(clientId);
            
            CheckMissionComplete();
            Debug.Log($"MissionsRecorder: target entered {targetType} by client {clientId}");
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
                        Debug.Log($"MissionsRecorder: {config.targetType} too far from {other.targetType}: {dist}");
                        return;
                    }
                }

                _recordedTargets.Add(config.targetType);
            }

            if (_recordedTargets.Count >= requiredTargets.Length)
            {
                _missionManager.CompleteMainMission();
                Debug.Log("MissionsRecorder: All targets recorded and close enough, mission Complete!");
            }
        }
    }
}