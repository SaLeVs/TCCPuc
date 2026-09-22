using System;
using System.Collections.Generic;
using Chat;
using Enums;
using UnityEngine;

namespace ScriptableObjects
{
    [Serializable]
    public class MoodChatEntry
    {
        public ChatMood mood;

        [Tooltip("Small talk for this mood, nothing here that reacts to a specific event - these fire when nothing is happening")]
        public TargetChatData data = new();

        [Min(0f)]
        [Tooltip("Seconds between ambient lines in this mood, before the audience multiplier applies.")]
        public float secondsBetweenLines = 9f;
    }

    /// <summary>
    /// The background hum. A real stream chat is never silent, so when nothing has happened for a
    /// while this keeps a slow trickle going, in whatever tone the room is currently in.
    /// </summary>
    [CreateAssetMenu(fileName = "New chat ambient database", menuName = "ScriptableObjects/Game/ChatAmbientDatabase")]
    public class ChatAmbientDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<MoodChatEntry> entries = new();

        private Dictionary<ChatMood, MoodChatEntry> _lookup;

        private void OnEnable() => BuildLookup();

        private void OnValidate() => BuildLookup();

        private void BuildLookup()
        {
            _lookup = new Dictionary<ChatMood, MoodChatEntry>();

            if (entries == null) return;

            foreach (MoodChatEntry entry in entries)
            {
                if (entry == null) continue;

                if (!_lookup.TryAdd(entry.mood, entry))
                {
                    Debug.LogWarning($"{name}: two entries for {entry.mood}. Keeping the first.", this);
                    continue;
                }

                entry.data?.ResetRuntimeState();
            }
        }

        /// <summary>
        /// Moods where some personality has almost nothing it is allowed to say.
        /// </summary>
        public IEnumerable<string> NarrowPools(int minimum)
        {
            if (entries == null) yield break;

            foreach (MoodChatEntry entry in entries)
            {
                if (entry?.data == null) continue;

                ViewerArchetype narrow = entry.data.NarrowArchetypes(minimum);

                if (narrow != ViewerArchetype.None) yield return $"{entry.mood} ({narrow})";
            }
        }

        public bool TryGet(ChatMood mood, out MoodChatEntry entry)
        {
            _lookup ??= new Dictionary<ChatMood, MoodChatEntry>();

            return _lookup.TryGetValue(mood, out entry);
        }
    }
}
