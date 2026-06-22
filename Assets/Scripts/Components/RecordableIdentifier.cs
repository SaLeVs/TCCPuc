using Enums;
using UnityEngine;

namespace Components
{
    public class RecordableIdentifier : MonoBehaviour
    {
        [SerializeField] private string recordableId;
        public string RecordableId => recordableId;
        
        public RecordableTarget targetType;
        public float minimumViewTime = 2f;
        
        public float audienceGain = 50f;
        public float reviewCooldown = 60f;
        public bool canBeReviewed;

        public bool canBeReviewedForChat = true;
        public float chatCooldown = 20f;
    }
}