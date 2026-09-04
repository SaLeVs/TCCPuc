using UnityEngine;

namespace ScriptableObjects
{
    [CreateAssetMenu(fileName = "New Impact", menuName = "ScriptableObjects/Game/ImpactProfile")]
    public class ImpactProfileSO : ScriptableObject
    {
        [Header("Gate")]
        [Tooltip("Speed the hitting object must be moving at, in units per second, for the impact to count at all. Keeps a player who merely walks into a still door from being knocked over.")]
        public float minimumSpeed = 0.5f;

        [Header("Damage")]
        [Tooltip("Damage dealt on impact. Leave at 0 for something that only knocks the player down.")]
        public float damage;

        [Header("Knockdown")]
        public bool knocksDown = true;

        [Tooltip("How long the player stays on the floor before getting back up.")]
        public float knockdownSeconds = 3f;

        [Tooltip("Push applied to the ragdoll along the impact direction.")]
        public float impulseForce = 6f;

        [Tooltip("Extra upward push, so the player is thrown rather than dragged along the floor.")]
        public float upwardForce = 2f;
    }
}
