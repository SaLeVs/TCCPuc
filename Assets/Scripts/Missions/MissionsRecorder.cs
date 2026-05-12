using System.Collections.Generic;
using Enums;
using Missions;
using Unity.Netcode;
using UnityEngine;

namespace Missions
{
    public class MissionsRecorder : MainMission
    {
        [SerializeField] private float maxDistance = 3f;
        [SerializeField] private RecordableTarget[] requiredTargets;

        private Dictionary<RecordableTarget, HashSet<ulong>> _playersWatching = new();
        private HashSet<RecordableTarget> _recordedTargets = new();

        private bool _monsterRecorded;
        private bool _stageRecorded;

        public override void StartMission()
        {
            if (!IsServer) return;
            
            foreach (RecordableTarget target in requiredTargets)
            {
                _playersWatching[target] = new HashSet<ulong>();
            }

            Debug.Log("MissionsRecorder: Mission started!");
        }

        public override void AbortMission()
        {
            if (!IsServer) return;

            _playersWatching.Clear();
            _recordedTargets.Clear();
        }

        public void ReportTargetEnter(ulong clientId, RecordableTarget targetType, float distance)
        {
            if (!IsServer) return;
            if (distance > maxDistance) return;
            if (!_playersWatching.ContainsKey(targetType)) return;

            _playersWatching[targetType].Add(clientId);
            CheckMissionComplete();
        }

        public void ReportTargetExit(ulong clientId, RecordableTarget targetType)
        {
            if (!IsServer) return;
            if (!_playersWatching.ContainsKey(targetType)) return;

            _playersWatching[targetType].Remove(clientId);
        }

        private void CheckMissionComplete()
        {
            foreach (RecordableTarget target in requiredTargets)
            {
                if (_playersWatching[target].Count > 0)
                {
                    _recordedTargets.Add(target);
                }
            }
            
            if (_recordedTargets.Count >= requiredTargets.Length)
            {
                Debug.Log("MissionsRecorder: All targets recorded — Mission Complete!");
            }
        }
    }
}

