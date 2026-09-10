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
        
        [Tooltip("Hard gate: below this number of viewers this donation NEVER fires, whatever the " +
                 "other weights say")]
        public int minViewersRequired = 0;
        
        [Tooltip("Above this number of viewers, the audience factor saturates at 1")]
        public int maxViewersConsidered = 500;
        
        [Tooltip("Base chance (0-1) used at each evaluation, before applying the weights above")]
        [Range(0f, 1f)] public float baseChance = 0.15f;
        
        [Tooltip("Minimum time in seconds between two donations of this same type")]
        public float cooldownSeconds = 45f;

        /// <summary>
        /// Chance (0-1) that this donation fires on a single evaluation, given the audience.
        ///
        /// <para>The formula, in order:</para>
        /// <list type="number">
        /// <item><b>Gate.</b> Below <see cref="minViewersRequired"/> the answer is 0, full stop.
        /// This used to be only the lower bound of the normalisation, which meant a donation
        /// asking for 100 viewers still fired at 16.7% with an empty theatre.</item>
        ///
        /// <item><b>Audience factor.</b> How far the audience has travelled from the gate to
        /// <see cref="maxViewersConsidered"/>, as 0..1. At the gate it is 0; at saturation, 1.</item>
        ///
        /// <item><b>Blend.</b> A weighted average between that factor and a flat 1.0:
        /// <c>(factor·viewerWeight + 1·randomWeight) / (viewerWeight + randomWeight)</c>.
        /// Read <see cref="randomWeight"/> as "share of the chance the audience cannot take
        /// away" — it is a floor. With viewerWeight 0.2 and randomWeight 0.5 that floor is
        /// 0.5/0.7 = 71% of baseChance, which is why the audience barely moved the needle.
        /// For the audience to really matter, randomWeight has to come down.</item>
        ///
        /// <item><b>Scale.</b> Multiply by <see cref="baseChance"/> — the chance at full house.</item>
        /// </list>
        /// </summary>
        public float EvaluateChance(int currentViewers)
        {
            // 1. Hard gate.
            if (currentViewers < minViewersRequired) return 0f;

            // 2. Audience factor, 0 at the gate and 1 at saturation.
            float range = Mathf.Max(1f, maxViewersConsidered - minViewersRequired);
            float viewerFactor = Mathf.Clamp01((currentViewers - minViewersRequired) / range);

            // 3. Blend towards a flat 1.0 by the random share.
            float totalWeight = Mathf.Max(0.0001f, viewerInfluenceWeight + randomWeight);
            float blended = (viewerFactor * viewerInfluenceWeight + randomWeight) / totalWeight;

            // 4. Scale by the ceiling.
            return Mathf.Clamp01(baseChance * blended);
        }

        /// <summary>
        /// The lowest chance this rule can produce once the gate is open — the part the audience
        /// can never remove. Useful for sanity-checking a rule in the inspector or a report.
        /// </summary>
        public float FloorChance
        {
            get
            {
                float totalWeight = Mathf.Max(0.0001f, viewerInfluenceWeight + randomWeight);
                return Mathf.Clamp01(baseChance * (randomWeight / totalWeight));
            }
        }
        
    }
}