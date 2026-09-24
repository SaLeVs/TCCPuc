using UnityEngine;

namespace Interfaces
{
    /// <summary>
    /// A door as the monster needs to see it. Lives here so Monster never has to reference the
    /// Objects assembly — Objects already reaches Player, and Player reaches back round to
    /// Monster, so a direct reference closes a cycle.
    /// </summary>
    public interface IForceableDoor
    {
        /// <summary>Centre of the doorway at floor height.</summary>
        Vector3 Position { get; }

        bool IsClosed { get; }

        /// <summary>True while the leaf is still moving, so nothing walks through it mid-swing.</summary>
        bool IsSwinging { get; }

        /// <summary>True while players cannot touch it. Expires on its own.</summary>
        bool IsLocked { get; }

        /// <summary>
        /// Where to stand to force the door from the side <paramref name="fromPosition"/> is on:
        /// centred on the doorway, <paramref name="standOff"/> metres back from the leaf, and the
        /// flat direction to face from there.
        /// </summary>
        void GetApproachPose(Vector3 fromPosition, float standOff, out Vector3 standPoint, out Vector3 facing);

        /// <summary>Opens the door away from whoever forced it. Server-side; never closes.</summary>
        void ForceOpenFrom(Vector3 fromPosition);

        /// <summary>
        /// Slams it shut and holds it that way. Server-side; the monster's sabotage. A doorway with
        /// someone standing in it is held open instead and shuts the moment it clears.
        /// </summary>
        void CloseAndLock(float seconds);

        /// <summary>Drops the hold early.</summary>
        void ClearLock();
    }
}
