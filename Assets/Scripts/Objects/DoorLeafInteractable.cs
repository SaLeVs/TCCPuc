using Interfaces;
using UnityEngine;

namespace Objects
{
    public class DoorLeafInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private Door door;

        public bool CanInteract(GameObject interactor) => door.CanInteract(interactor);
        public bool Interact(GameObject playerInteractor) => door.Interact(playerInteractor);
    }
}
