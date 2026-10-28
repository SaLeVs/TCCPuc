using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Item", menuName = "ScriptableObjects/Game/ItemData")]
    public class ItemDataSO : ScriptableObject
    {
        public int itemId;
        public string itemName;
        public Sprite icon;
        public GameObject prefabUsable;
        public GameObject prefabPickable;
        public GameObject prefabVisual;

        [Tooltip("Upper-body pose played while this item is in the hand (Humanoid clip, arms and fingers). " +
                 "Empty uses the generic hold set on the player's PlayerHoldAnimation.")]
        public AnimationClip holdAnimation;

        [Tooltip("Where the item sits in the hand, in metres from the centre of the right palm (the player's HandHolder): " +
                 "+Z toward the fingers, +Y out of the palm, +X toward the thumb.")]
        public Vector3 holdPosition;

        [Tooltip("Rotation of the item in the hand, in euler angles from the palm's axes (see Hold Position).")]
        public Vector3 holdRotation;
    }
}

