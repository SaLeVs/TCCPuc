using Inputs;
using Interfaces;
using Unity.Netcode;
using UnityEngine;

namespace Player
{
    public class PlayerCrounch : NetworkBehaviour, ISpeedModifier
    {
        [SerializeField] private InputReader inputReader;
        [SerializeField] private float speedModifier = 0.5f;
    
        private bool _isCrounching;
        
        
        public float ModifySpeed(float baseSpeed)
        {
            if (_isCrounching)
            {
                return baseSpeed * speedModifier;
                
            }

            return baseSpeed;
            
        }
        
        
    }  
}

