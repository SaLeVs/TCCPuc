using Enums;
using ScriptableObjects;
using UnityEngine;

namespace Missions.Donations
{
    [CreateAssetMenu(fileName = "DonationDefinition", menuName = "Missions/Donations/Donation Definition")]
    public class DonationDefinition : ScriptableObject
    {
        [Header("Identity")] 
        public string donationId;
        public string displayName;
        [TextArea] public string message;

        [Header("Category")]
        public DonationCategory category;
        
        [Header("Target (used only if category = Recording)")]
        public RecordableTarget recordingTarget;
        
        [Tooltip("Time (seconds) that some player needs to keep filming the target to complete")]
        public float requiredRecordingSeconds = 5f;

        [Header("Mic action for (MicSpeech)")]
        [Tooltip("Needs to match the micActionId configured in the relevant DonationMicWatcher")]
        public string micActionId = "default";
        
        [Tooltip("Time (seconds) of continuous speech required")]
        public float requiredSpeechSeconds = 3f;

        [Header("Donation amount (simulated)")]
        public float minAmount = 5f;
        public float maxAmount = 100f;

        [Header("Expiration")]
        [Tooltip("0 = never expires")]
        public float durationSeconds = 30f;

        [Header("Rules of engagement")]
        public DonationTriggerRule triggerRule = new DonationTriggerRule();

        [Header("Names of donators (flavor, optional)")]
        public ViewerNameDatabaseSO fakeDonorNames;
    }
}