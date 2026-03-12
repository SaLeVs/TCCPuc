using Systems;

namespace Interfaces
{
    public interface IInteractable
    {
        public bool CanInteract();
        public bool Interact(Interactor interactor);
    }
}
