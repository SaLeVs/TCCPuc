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
        [Tooltip("Id systems raise to reach this pool. Built-in ones look like hint.door_locked; " +
                 "a new topic can use any id, for example tutorial.welcome, and needs no code.")]
        public string id;

        [Tooltip("What chat says about this. Empty means the topic is raised but stays silent.")]
        public TargetChatData data = new();

        [Header("Volume")]
        [Min(0)] public int minMessages = 1;
        [Min(0)] public int maxMessages = 3;

        [Header("Reaction")]
        [Range(0f, 1f)]
        [Tooltip("Ceiling for how hard this stirs the chat. The raiser scales it by how big this " +
                 "particular instance was, where 0.5 means nominal. High values make the following " +
                 "lines come faster and hold the mood longer.")]
        public float intensity = 0.5f;

        [Tooltip("Mood this pushes the room into while the reaction lasts.")]
        public ChatMood mood = ChatMood.Idle;

        [Min(0f)]
        [Tooltip("Seconds before this topic can fire again. This is the throttle for anything that " +
                 "raises repeatedly, like the lights-out hint that re-raises while the lights are " +
                 "still off.")]
        public float cooldown = 4f;

        [Header("Delivery")]
        [Min(0)]
        [Tooltip("Lines with a higher priority jump ahead of lower ones in the queue and survive " +
                 "when the queue overflows. Hints sit above reactions, which sit above small talk, " +
                 "so advice is never buried under a wall of chatter.")]
        public int priority;

        [Tooltip("Allow a spam wave - several viewers posting the same short line at once. Only " +
                 "fires on lines flagged spammable, and only when the moment is intense.")]
        public bool allowSpamWave;

        [Tooltip("Say it even when nobody is watching. A hint is worth breaking the illusion for; " +
                 "small talk from an empty chat is not.")]
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
