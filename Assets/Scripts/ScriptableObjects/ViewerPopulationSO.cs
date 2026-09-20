using System;
using System.Collections.Generic;
using Enums;
using UnityEngine;

namespace ScriptableObjects
{
    [Serializable]
    public class ViewerProfile
    {
        public string name;

        [Tooltip("Personality. Decides which lines this viewer is allowed to post.")]
        public ViewerArchetype archetype = ViewerArchetype.Lurker;

        [Min(0f)]
        [Tooltip("How often this viewer talks relative to the others. Give a handful of regulars " +
                 "6-10 and leave the crowd at 0.5-1: a chat where everyone talks equally reads " +
                 "like a name generator, one with a few loud regulars reads like people.")]
        public float chattiness = 1f;
    }

    /// <summary>
    /// Who is watching. Replaces the old shuffled bag of names, which drained all 30 names before
    /// repeating anyone - so no viewer ever had a presence. Here a few regulars dominate, the rest
    /// drop in once, and nobody posts twice in a row.
    /// </summary>
    [CreateAssetMenu(fileName = "New viewer population", menuName = "ScriptableObjects/Game/ViewerPopulation")]
    public class ViewerPopulationSO : ScriptableObject
    {
        [SerializeField] private List<ViewerProfile> viewers = new();

        [SerializeField, Min(0)]
        [Tooltip("How many of the last speakers get pushed to the back of the queue. Stops the " +
                 "same regular answering themselves without banning them for long.")]
        private int recentMemory = 5;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Weight multiplier for someone who spoke inside the recent memory.")]
        private float recentPenalty = 0.15f;

        private readonly Queue<string> _recent = new();
        private readonly HashSet<string> _recentSet = new();

        public int Count => viewers?.Count ?? 0;

        private void OnEnable()
        {
            ResetRuntimeState();
        }

        public void ResetRuntimeState()
        {
            _recent.Clear();
            _recentSet.Clear();
        }

        /// <summary>
        /// Draws who speaks next, weighted by chattiness and biased away from the last few names.
        /// </summary>
        public bool TryPick(out ViewerProfile viewer)
        {
            viewer = null;

            int count = Count;
            if (count == 0) return false;

            float total = 0f;

            for (int i = 0; i < count; i++)
            {
                total += Weight(viewers[i]);
            }

            if (total <= 0f) return false;

            float roll = UnityEngine.Random.value * total;
            float cumulative = 0f;

            for (int i = 0; i < count; i++)
            {
                float weight = Weight(viewers[i]);
                if (weight <= 0f) continue;

                cumulative += weight;

                if (roll < cumulative)
                {
                    viewer = viewers[i];
                    Remember(viewer.name);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Same draw, for callers that only need the name - a donation naming its donor, for
        /// instance. Going through here rather than keeping a second list of names is what makes
        /// the person who donates and the person who talks the same person.
        /// </summary>
        public bool TryPickName(out string name)
        {
            name = null;

            if (!TryPick(out ViewerProfile profile)) return false;

            name = profile.name;

            return true;
        }

        private float Weight(ViewerProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.name)) return 0f;
            if (profile.chattiness <= 0f) return 0f;

            return _recentSet.Contains(profile.name)
                ? profile.chattiness * recentPenalty
                : profile.chattiness;
        }

        private void Remember(string name)
        {
            if (recentMemory <= 0) return;

            if (_recentSet.Add(name))
            {
                _recent.Enqueue(name);
            }

            while (_recent.Count > recentMemory)
            {
                _recentSet.Remove(_recent.Dequeue());
            }
        }
    }
}
