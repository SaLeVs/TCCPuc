using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Enums
{
    /// <summary>Why the cursor is currently free. Each owner holds its own, independent of the rest.</summary>
    public enum CursorReason
    {
        Paused,
        InputLocked,
        MissionUi,
        Spectating,
        GameOver,
        Victory,
        MainMenu
    }

    /// <summary>
    /// The single owner of the hardware cursor.
    ///
    /// Callers do not say "show the cursor", they say "I have a reason for it to be free" and later
    /// drop that reason. The cursor is free while at least one reason stands. That is the whole
    /// point: closing the pause menu while the player is locked at a board used to hide the cursor,
    /// because whoever closed the menu asserted an absolute state without knowing about the lock.
    /// Here, that same close simply drops <see cref="CursorReason.Paused"/> and the cursor stays
    /// free, because <see cref="CursorReason.InputLocked"/> is still held.
    /// </summary>
    public static class CursorState
    {
        private static readonly HashSet<CursorReason> Held = new();

        private static bool _hooked;

        public static bool IsFree => Held.Count > 0;

        /// <summary>Takes or drops one reason and re-applies whatever the set now adds up to.</summary>
        public static void Set(CursorReason reason, bool held)
        {
            Hook();

            bool changed = held ? Held.Add(reason) : Held.Remove(reason);
            if (!changed) return;

            Apply();
        }

        public static void Hold(CursorReason reason) => Set(reason, true);

        public static void Release(CursorReason reason) => Set(reason, false);

        /// <summary>
        /// Forces the hardware cursor to match the set. For whoever establishes the starting
        /// state, before any reason has been taken.
        /// </summary>
        public static void Refresh() => Apply();

        /// <summary>Drops everything. Used between scenes so no reason outlives whoever held it.</summary>
        public static void Clear()
        {
            if (Held.Count == 0) return;

            Held.Clear();
            Apply();
        }

        private static void Apply()
        {
            bool free = IsFree;

            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        /// <summary>
        /// A reason is only ever dropped by the thing that took it, and that thing does not survive
        /// a scene change - so the set is emptied on load instead of leaking into the next scene.
        /// </summary>
        private static void Hook()
        {
            if (_hooked) return;

            _hooked = true;
            SceneManager.sceneLoaded += (_, _) => Clear();
        }

        // Static state survives entering play mode when domain reload is off, so it is wiped here.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Held.Clear();
            _hooked = false;
        }
    }
}
