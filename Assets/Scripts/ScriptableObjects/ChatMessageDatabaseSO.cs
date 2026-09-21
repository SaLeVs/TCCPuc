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
        [SerializeField] private List<TargetChatEntry> entries = new();

        private Dictionary<RecordableTarget, TargetChatData> _lookup;


        private void OnEnable() => BuildLookup();
        private void OnValidate() => BuildLookup();

        private void BuildLookup()
        {
            _lookup = new Dictionary<RecordableTarget, TargetChatData>();

            if (entries == null) return;

            foreach (TargetChatEntry entry in entries)
            {
                if (entry == null) continue;

                if (!_lookup.TryAdd(entry.target, entry.data))
                {
                    Debug.LogWarning($"{name}: two entries for {entry.target}. Keeping the first, the second is unreachable.", this);
                    continue;
                }

                entry.data?.ResetRuntimeState();
            }
        }

        public bool TryGetData(RecordableTarget target, out TargetChatData data)
        {
            _lookup ??= new Dictionary<RecordableTarget, TargetChatData>();

            return _lookup.TryGetValue(target, out data);
        }

        /// <summary>
        /// Targets that exist in the enum but have no pool here. Called on startup so a target
        /// wired onto an object with nothing to say shows up once as a warning instead of as
        /// silence the whole match.
        /// </summary>
        /// <summary>
        /// Targets where some personality has almost nothing it is allowed to say. Same failure as
        /// an empty pool, but invisible: the pool looks full and one personality still repeats.
        /// </summary>
        public IEnumerable<string> NarrowPools(int minimum)
        {
            if (entries == null) yield break;

            foreach (TargetChatEntry entry in entries)
            {
                if (entry?.data == null || entry.data.Count == 0) continue;

                ViewerArchetype narrow = entry.data.NarrowArchetypes(minimum);

                if (narrow != ViewerArchetype.None) yield return $"{entry.target} ({narrow})";
            }
        }

        public IEnumerable<RecordableTarget> MissingTargets()
        {
            _lookup ??= new Dictionary<RecordableTarget, TargetChatData>();

            foreach (RecordableTarget target in Enum.GetValues(typeof(RecordableTarget)))
            {
                if (target == RecordableTarget.None) continue;

                if (!_lookup.TryGetValue(target, out TargetChatData data) || data == null || data.Count == 0)
                {
                    yield return target;
                }
            }
        }
    }

    [Serializable]
    public class TargetChatEntry
    {
        public RecordableTarget target;
        public TargetChatData data = new();
    }
}
