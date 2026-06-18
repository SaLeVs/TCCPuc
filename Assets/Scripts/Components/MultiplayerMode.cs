using UnityEngine;

namespace Components
{
    public enum SessionMode
    {
        Online,
        Lan
    }
    public static class MultiplayerModeManager
    {
        public static SessionMode CurrentMode { get; private set; } = SessionMode.Online;

        public static bool IsOnline => CurrentMode == SessionMode.Online;
        public static bool IsLan => CurrentMode == SessionMode.Lan;

        public static void SetOnline() => CurrentMode = SessionMode.Online;
        public static void SetLan() => CurrentMode = SessionMode.Lan;
    }

}
