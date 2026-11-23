namespace Interfaces
{
    /// <summary>
    /// The name a player picked, readable from assemblies that can't see the Player one (the
    /// lobby ready board lists the cast with it). Lives on the player object.
    /// </summary>
    public interface IPlayerDisplayName
    {
        public string DisplayName { get; }
    }
}
