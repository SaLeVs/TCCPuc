using System;
using System.Collections.Generic;
using Chat;
using Enums;
using UnityEngine;


namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New chat message List", menuName = "ScriptableObjects/Game/ChatMessageDatabase")]
    public class ChatMessageDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<TargetChatEntry> entries;
        
        private Dictionary<RecordableTarget, TargetChatData> _lookup;

        
        private void OnEnable()
        { 
            BuildLookup();
        }

        private void BuildLookup()
        {
            _lookup = new();

            foreach (var entry in entries)
            {
                _lookup[entry.target] = entry.data;
            }
        }

        public bool TryGetData(RecordableTarget target, out TargetChatData data)
        {
            return _lookup.TryGetValue(target, out data);
        }
    }

    [Serializable]
    public class TargetChatEntry
    {
        public RecordableTarget target;
        public TargetChatData data;
    }
}

