using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerState : NetworkBehaviour
    {
        public Vector2 speed { get; set; }
        public bool isRunning { get; set; }
        public bool isCrouching { get; set; }
        
    }
}