using System;
using System.Collections.Generic;

namespace Monster.MonsterSabotages
{
    /// <summary>
    /// Every lamp that is live right now, regular and emergency kept apart. Lamps announce
    /// themselves as they load, so the ones inside a room prefab - instantiated by SpawnRooms once
    /// the match is already running - end up here the same way as the ones placed in the scene.
    /// MonsterSabotage copies both lists when it spawns and listens for whatever comes after.
    /// </summary>
    public static class LightRegistry
    {
        private static readonly List<LightSabotage> Regular = new List<LightSabotage>();
        private static readonly List<LightSabotage> Emergency = new List<LightSabotage>();

        public static IReadOnlyList<LightSabotage> Lights => Regular;
        public static IReadOnlyList<LightSabotage> EmergencyLights => Emergency;

        /// <summary>
        /// A lamp that has just come alive. Raised again when one is switched back on after being
        /// disabled, so whoever owns the blackout can bring it up to date.
        /// </summary>
        public static event Action<LightSabotage> OnRegistered;

        public static void Register(LightSabotage lamp)
        {
            if (lamp == null) return;

            List<LightSabotage> list = lamp.IsEmergency ? Emergency : Regular;
            if (!list.Contains(lamp)) list.Add(lamp);

            OnRegistered?.Invoke(lamp);
        }

        public static void Unregister(LightSabotage lamp)
        {
            // Both, not just the one its flag points at: a destroyed lamp compares equal to null,
            // and the flag could have been flipped in the Inspector while it was listed.
            Regular.Remove(lamp);
            Emergency.Remove(lamp);
        }
    }
}
