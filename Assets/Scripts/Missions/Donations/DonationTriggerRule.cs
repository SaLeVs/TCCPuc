using System;
using UnityEngine;

namespace Missions.Donations
{
    [Serializable]
    public class DonationTriggerRule
    {
        [Header("Proportion with audience")]
        [Tooltip("How much the number of viewers influences the chance (0 = ignores viewers completely)")]
        [Range(0f, 1f)] public float viewerInfluenceWeight = 0.5f;
        
        [Tooltip("How much the chance is pure randomness (0 = never random)")]
        [Range(0f, 1f)] public float randomWeight = 0.5f;
        
        [Tooltip("Below this number of viewers, the audience factor is zero")]
        public int minViewersRequired = 0;
        
        [Tooltip("Above this number of viewers, the audience factor saturates at 1")]
        public int maxViewersConsidered = 500;
        
        [Tooltip("Base chance (0-1) used at each evaluation, before applying the weights above")]
        [Range(0f, 1f)] public float baseChance = 0.15f;
        
        [Tooltip("Minimum time in seconds between two donations of this same type")]
        public float cooldownSeconds = 45f;

        /// <summary>
        /// Calculate the final chance (0-1) of triggering this donation, given the current number of viewers.
        /// </summary>
        public float EvaluateChance(int currentViewers)
        {
            float range = Mathf.Max(1, maxViewersConsidered - minViewersRequired);
            float viewerFactor = Mathf.Clamp01((currentViewers - minViewersRequired) / range);

            float totalWeight = Mathf.Max(0.0001f, viewerInfluenceWeight + randomWeight);
            float blended = (viewerFactor * viewerInfluenceWeight + 1f * randomWeight) / totalWeight;

            return Mathf.Clamp01(baseChance * blended);
        }
        
    }
}