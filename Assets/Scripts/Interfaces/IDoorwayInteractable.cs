using UnityEngine;

namespace Interfaces
{
    /// <summary>
    /// A door whose doorway counts as the door itself, open or shut. An open leaf stands off to
    /// the side, edge-on or behind whoever just ran through, and aiming at it on the run was the
    /// only way to close it. The opening is what the player actually looks at.
    /// </summary>
    public interface IDoorwayInteractable : IInteractable
    {
        /// <summary>
        /// Where <paramref name="ray"/> enters the doorway, no further than
        /// <paramref name="maxDistance"/>. <paramref name="leaf"/> is the collider of the leaf
        /// that fills that part of it when shut, so it can be highlighted.
        /// </summary>
        public bool TryRaycastDoorway(Ray ray, float maxDistance, out float distance, out Collider leaf);
    }
}
