namespace Interfaces
{
    /// <summary>
    /// Optional companion to <see cref="IInteractable"/>: the verb the HUD prints next to the key,
    /// when it depends on who is looking (the host picks the contract, everyone else votes).
    /// Empty or null falls back to the label set on the marker.
    /// </summary>
    public interface IInteractionLabel
    {
        public string InteractionLabel { get; }
    }
}
