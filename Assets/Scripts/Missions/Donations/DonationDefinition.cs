 using Enums;
using ScriptableObjects;
using UnityEngine;

namespace Missions.Donations
{
    [CreateAssetMenu(fileName = "DonationDefinition", menuName = "ScriptableObjects/Donations/Donation Definition")]
    public class DonationDefinition : ScriptableObject
    {
        [Header("Identity")] 
        public string donationId;
        public string displayName;
        [TextArea] public string message;

        [Header("Category")]
        public DonationCategory category;
        
        [Header("Target")]
        [Tooltip("Recording: required. MicSpeech: optional — None means the scream can happen anywhere (old behavior); any other value requires the player to also be looking at/near a target of that type.")]
        public RecordableTarget targetType;
        
        [Tooltip("Time (seconds) that some player needs to keep filming the target to complete")]
        public float requiredRecordingSeconds = 5f;

        [Header("Mic action for (MicSpeech)")]
        [Tooltip("Needs to match the micActionId configured in the relevant DonationMicWatcher")]
        public string micActionId = "default";
        
        [Tooltip("Time (seconds) of continuous speech required")]
        public float requiredSpeechSeconds = 3f;

        [Header("Donation amount (simulated)")]
        public float minAmountMoney = 5f;
        public float maxAmountMoney = 100f;

        [Header("Expiration")]
        [Tooltip("0 = never expires")]
        public float durationSeconds = 30f;

        [Header("Rules of engagement")]
        public DonationTriggerRule triggerRule = new DonationTriggerRule();
        
        [Header("Stacking mode")]
        [Tooltip("Cumulative = can receive multiple donates of this type at the same time. Exclusive = only 1 at a time.")]
        public DonationStackingMode stackingMode = DonationStackingMode.Cumulative;

        [Header("Names of donators (flavor, optional)")]
        public ViewerNameDatabaseSO fakeDonorNames;
    }
}