using System;
using UnityEngine;

namespace Components
{
    /// <summary>
    /// Server-side: a player has just gone down, killed or knocked over. Raised at the moment it
    /// happens, before the body leaves anyone's vision — so the monster can still tell whether it
    /// happened in front of it.
    ///
    /// <para>Static, like <see cref="NavMeshRebuildNotifier"/>: Monster must not reference Player.</para>
    /// </summary>
    public static class PlayerDownedNotifier
    {
        /// <summary>The player's root object, and true when they died rather than being knocked down.</summary>
        public static event Action<GameObject, bool> OnPlayerDowned;

        public static void Notify(GameObject player, bool died)
        {
            OnPlayerDowned?.Invoke(player, died);
        }
    }
}
