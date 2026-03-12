using UnityEngine;

namespace Interfaces
{
    public interface IInteractable
    {
        public bool CanInteract();
        public bool Interact(GameObject  playerInteractor);
    }
}
