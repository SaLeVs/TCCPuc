using Enums;
using UnityEngine;

namespace Components
{
    public class RecordableIdentifier : MonoBehaviour
    {
        public RecordableTarget targetType;
        public float audienceGain = 50f;
        public float minimumViewTime = 2f;
        public float reviewCooldown = 60f;
        public bool canBeReviewed;
    }
}