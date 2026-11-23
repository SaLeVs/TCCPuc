using UnityEngine;

namespace Interfaces
{
    /// <summary>
    /// Optional companion to <see cref="IInteractable"/>: tells the HUD why the object refuses the
    /// player, shown under the "unavailable" prompt. Empty or null keeps the plain prompt.
    /// </summary>
    public interface IInteractionBlockReason
    {
        public string GetBlockedReason(GameObject interactor);
    }
}
