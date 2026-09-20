namespace Enums
{
    /// <summary>
    /// What the room feels like right now. The director picks one every frame from the audience
    /// level and recent stimuli, and it decides which ambient pool fills the silence.
    /// </summary>
    public enum ChatMood
    {
        /// <summary>Default. Random small talk.</summary>
        Idle,

        /// <summary>Audience is decaying because nothing is happening. Complaints.</summary>
        Bored,

        /// <summary>The monster is in frame or chasing. Screaming.</summary>
        Panic
    }
}
