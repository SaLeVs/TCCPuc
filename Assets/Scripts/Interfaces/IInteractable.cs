using UnityEngine;

namespace Interfaces
{
    public interface IInteractable
    {
        public bool CanInteract(GameObject interactor);
        public bool Interact(GameObject  playerInteractor);
    }
}
