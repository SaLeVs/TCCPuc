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

            _checkTimer += Time.deltaTime;
            if (_checkTimer >= checkInterval)
            {
                _checkTimer = 0f;
                CheckMissionComplete();
            }
        }
        
        private void CheckMissionComplete()
        {
            foreach (RecordableTargetConfig config in requiredTargets)
            {
                if (!_playersWatching.TryGetValue(config.targetType, out var viewers)) return;
                if (viewers.Count == 0) return;
                if (!_targetObjects.ContainsKey(config.targetType)) return;
            }
            
            for (int i = 0; i < requiredTargets.Length; i++)
            {
                RecordableTargetConfig current = requiredTargets[i];

                GameObject currentObj = _targetObjects[current.targetType];

                for (int j = i + 1; j < requiredTargets.Length; j++)
                {
                    RecordableTargetConfig other = requiredTargets[j];

                    GameObject otherObj = _targetObjects[other.targetType];

                    float distance = Vector3.Distance(currentObj.transform.position, otherObj.transform.position);

                    if (distance > current.maxDistanceToOtherTargets) return;
                }
            }

            _missionManager.CompleteMainMission();
        }
        
    }
}
        