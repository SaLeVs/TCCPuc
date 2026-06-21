using Enums;
using UnityEngine;

namespace Components
{
    public class RecordableIdentifier : MonoBehaviour
    {
        public RecordableTarget targetType;
        public float minimumViewTime = 2f;
        
        public float audienceGain = 50f;
        public float reviewCooldown = 60f;
        public bool canBeReviewed;

        public bool canBeReviewedForChat = true;
        public float chatCooldown = 20f;
        
    }
}