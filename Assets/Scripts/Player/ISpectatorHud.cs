namespace Player
{
    /// <summary>
    /// What the HUD does while its player spectates: show the one being watched - their chat,
    /// missions and life - instead of going dark. Implemented in the UI assembly, found by
    /// <see cref="PlayerSpectator"/> on its own object; without one the old behaviour stays and the
    /// gameplay HUD is simply turned off.
    /// </summary>
    public interface ISpectatorHud
    {
        /// <summary>Spectating begins: hide what only means something for a player still in the building.</summary>
        void EnterSpectating();

        /// <summary>The player now being watched. Null when there is nobody left to watch.</summary>
        void Watch(PlayerState target);
    }
}
