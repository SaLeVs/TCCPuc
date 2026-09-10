using UnityEngine;

namespace Interfaces
{
    /// <summary>
    /// Something the monster's AI should treat as a live target worth walking towards.
    ///
    /// <para>Exists because the Monster assembly does not reference Player — this is the seam
    /// that lets the AI ask "who is still in play?" without knowing what a player is.</para>
    /// </summary>
    public interface IHuntable
    {
        /// <summary>
        /// False once the target is out of the game — dead, or escaped. A corpse left on the
        /// floor should stop attracting the monster.
        /// </summary>
        bool IsHuntable { get; }

        Vector3 HuntablePosition { get; }
    }
}
