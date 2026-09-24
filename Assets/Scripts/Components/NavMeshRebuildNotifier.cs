using System;

namespace Components
{
    /// <summary>
    /// Raised on a peer right after it rebuilds the navmesh at runtime. Anything that sampled or
    /// linked against the old data — the doors' fallback links, for one — has to look again.
    ///
    /// <para>Static rather than a reference to the surface, so Objects can listen without
    /// depending on Rooms.</para>
    /// </summary>
    public static class NavMeshRebuildNotifier
    {
        public static event Action OnRebuilt;

        public static void NotifyRebuilt()
        {
            OnRebuilt?.Invoke();
        }
    }
}
