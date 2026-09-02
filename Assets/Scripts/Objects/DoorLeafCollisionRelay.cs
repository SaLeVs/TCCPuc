using System;
using UnityEngine;

namespace Objects
{
    public class DoorLeafCollisionRelay : MonoBehaviour
    {
        public event Action<Collision> OnLeafCollisionEnter;

        private void OnCollisionEnter(Collision collision)
        {
            OnLeafCollisionEnter?.Invoke(collision);
        }
    }
}