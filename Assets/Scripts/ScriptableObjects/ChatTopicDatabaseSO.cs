using System;
using System.Collections.Generic;
using Chat;
using Enums;
using UnityEngine;

namespace ScriptableObjects
{
    [Serializable]
    public class ChatTopicEntry
    {
        [Tooltip("Id used to raise this topic, must be unique.")]
        public string id;

        [Tooltip("What chat says about this topic.")]
        public TargetChatData data = new();

        [Header("Volume")]
        [Min(0)] public int minMessages = 1;
        [Min(0)] public int maxMessages = 3;

        [Header("Reaction")]
        [Range(0f, 1f)]
        [Tooltip("How intense the moment is when this topic fires")]
        public float intensity = 0.5f;

        [Tooltip("Control which ambient pool fills the silence and which weight multipliers apply to reactions.")]
        public ChatMood mood = ChatMood.Idle;

        [Min(0f)]
        public float cooldown = 4f;

        [Header("Delivery")]
        [Min(0)]
        [Tooltip("Lines with a higher priority jump ahead of lower ones in the queue")]
        public int priority;

        [Tooltip("Allow a spam wave - several viewers posting the same short line at once")]
        public bool allowSpamWave;

        [Tooltip("Say it even when nobody is watching.")]
        public bool ignoreViewerFloor;
    }

    /// <summary>
    /// Everything the chat can say about things that happen, keyed by a plain string id.
    ///
    /// <para>String ids rather than an enum so the set of things the chat reacts to is data, not
    /// code. A tutorial, a new hint or a scripted beat is a row here plus lines, raised from
    /// anywhere with one static call - no recompile, no reference to this assembly, no cycle to
    /// work around.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "New chat topic database", menuName = "ScriptableObjects/Game/ChatTopicDatabase")]
    public class ChatTopicDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<ChatTopicEntry> entries = new();

        private Dictionary<string, ChatTopicEntry> _lookup;

        private void OnEnable() => BuildLookup();

        // Rebuilt here too so editing the asset while the game runs takes effect on the next line
        // instead of silently doing nothing until a domain reload.
        private void OnValidate() => BuildLookup();

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, ChatTopicEntry>(StringComparer.OrdinalIgnoreCase);

            if (entries == null) return;

            foreach (ChatTopicEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id)) continue;

                if (!_lookup.TryAdd(entry.id.Trim(), entry))
                {
                    Debug.LogWarning($"{name}: two entries for '{entry.id}'. Keeping the first, " +
                                     "the second is unreachable.", this);
                    continue;
                }

                entry.data?.ResetRuntimeState();
            }
        }

        public bool TryGet(string topicId, out ChatTopicEntry entry)
        {
            entry = null;

            if (string.IsNullOrWhiteSpace(topicId)) return false;

            _lookup ??= new Dictionary<string, ChatTopicEntry>(StringComparer.OrdinalIgnoreCase);

            return _lookup.TryGetValue(topicId.Trim(), out entry);
        }

        /// <summary>
        /// Ids that have a row but no lines. Reported once at startup so a topic someone wired and
        /// never wrote for shows up as a warning instead of as silence nobody can explain.
        /// </summary>
        public IEnumerable<string> EmptyTopics()
        {
            if (entries == null) yield break;

            foreach (ChatTopicEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id)) continue;

                if (entry.data == null || entry.data.Count == 0)
                {
                    yield return entry.id;
                }
            }
        }
    }
}
