using Interfaces;
using UnityEngine;

namespace Objects
{
    public class DoorLeafInteractable : MonoBehaviour, IDoorwayInteractable, IInteractionRange
    {
        [SerializeField] private Door door;

        public float InteractionRange => door.InteractionRange;

        public bool CanInteract(GameObject interactor) => door.CanInteract(interactor);
        public bool Interact(GameObject playerInteractor) => door.Interact(playerInteractor);

        public bool TryRaycastDoorway(Ray ray, float maxDistance, out float distance, out Collider leaf) =>
            door.TryRaycastDoorway(ray, maxDistance, out distance, out leaf);
    }
}
