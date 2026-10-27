namespace Interfaces
{
    /// <summary>
    /// An interactable that can be used from further away than the interactor's default reach.
    /// Doors ask for it: a player running past one has only a moment to catch it, and raising the
    /// reach for everything would let items be grabbed from across the room.
    /// </summary>
    public interface IInteractionRange
    {
        public float InteractionRange { get; }
    }
}
