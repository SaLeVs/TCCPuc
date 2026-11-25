namespace Network
{
    /// <summary>
    /// Whether the Unity services (sign-in, Relay, Lobby, Vivox) answered. False on a network that
    /// blocks them - the game still opens and LAN still works, only online play and voice are off.
    /// </summary>
    public static class OnlineServices
    {
        /// <summary>Signed in: Relay and Lobby can be used.</summary>
        public static bool IsAvailable => AuthenticationController.CurrentAuthentaticationState == AuthenticationState.Authenticated;

        /// <summary>Vivox initialised. Set once, after signing in.</summary>
        public static bool IsVoiceAvailable { get; set; }
    }
}
