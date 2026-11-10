using System;
using System.Collections.Generic;

namespace Monster.MonsterSabotages
{
    /// <summary>
    /// Sabotage targets that only exist once a match is running. MonsterSabotage's serialized list
    /// is filled in the scene, so it can only ever hold things placed by hand — everything inside a
    /// room prefab is instantiated by SpawnRooms and has to announce itself instead.
    /// </summary>
    public static class SabotageRegistry
    {
        private static readonly List<ISabotageable> Targets = new List<ISabotageable>();

        public static IReadOnlyList<ISabotageable> All => Targets;

        /// <summary>
        /// A target that has just joined, already in <see cref="All"/>. A room spawned in the
        /// middle of a blackout brings lamps that know nothing of it, and they would be the only
        /// lit thing in the level — MonsterSabotage listens here and puts them out.
        /// </summary>
        public static event Action<ISabotageable> OnRegistered;

        public static void Register(ISabotageable target)
        {
            if (target == null || Targets.Contains(target)) return;

            Targets.Add(target);
            OnRegistered?.Invoke(target);
        }

        public static void Unregister(ISabotageable target)
        {
            if (target == null) return;

            Targets.Remove(target);
        }
    }
}
