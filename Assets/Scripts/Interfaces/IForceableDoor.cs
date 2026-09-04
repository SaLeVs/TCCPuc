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
        Vector3 Position { get; }

        bool IsClosed { get; }

        /// <summary>True while the leaf is still moving, so nothing walks through it mid-swing.</summary>
        bool IsSwinging { get; }

        /// <summary>True while players cannot touch it. Expires on its own.</summary>
        bool IsLocked { get; }

        /// <summary>Opens the door away from whoever forced it. Server-side; never closes.</summary>
        void ForceOpenFrom(Vector3 fromPosition);

        /// <summary>Slams it shut and holds it that way. Server-side; the monster's sabotage.</summary>
        void CloseAndLock(float seconds);

        /// <summary>Drops the hold early.</summary>
        void ClearLock();
    }
}
