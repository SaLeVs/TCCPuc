using UnityEngine;

namespace Interfaces
{
    public interface IUsable
    {
        public bool CanUse(GameObject playerInteractor);
        public void Use(GameObject playerInteractor);
    }
}