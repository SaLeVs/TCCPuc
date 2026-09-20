namespace Enums
{
    /// <summary>
    /// What the room feels like right now. The director picks one every frame from audience level
    /// and recent stimuli, and it decides which ambient pool fills the silence plus which weight
    /// multipliers apply to reaction lines.
    /// </summary>
    public enum ChatMood
    {
        /// <summary>Nothing happening, audience healthy. Small talk.</summary>
        Idle,

        /// <summary>Audience is decaying. Complaints, "vai fazer alguma coisa".</summary>
        Bored,

        /// <summary>Something good just happened. Cheering.</summary>
        Hype,

        /// <summary>Danger is near but nothing has happened yet. Warnings.</summary>
        Tense,

        /// <summary>It happened. Screaming.</summary>
        Panic
    }
}
