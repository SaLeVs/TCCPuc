using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Inputs
{
    [CreateAssetMenu(fileName = "InputReader", menuName = "ScriptableObjects/Inputs/InputReader")]
    public class InputReader : ScriptableObject, PlayerActions.IGameActions
    {
        private PlayerActions _playerActions;
    
        public event Action<Vector2> OnMoveEvent;
        public event Action<Vector2> OnCameraLookEvent;
    
        public event Action<bool> OnRunEvent;
        public event Action<bool> OnCrouchEvent;
        public event Action<bool> OnFlashlightEvent;
        public event Action<bool> OnInteractEvent;
        public event Action<bool> OnMapEvent;


        private void OnEnable()
        {
            if (_playerActions == null)
            {
                _playerActions = new PlayerActions();
                _playerActions.Game.SetCallbacks(this);
            }

            _playerActions.Game.Enable();
        
        }

        
        public void OnMove(InputAction.CallbackContext context)
        {
            OnMoveEvent?.Invoke(context.ReadValue<Vector2>());
            
        }
        
        public void OnCameraLook(InputAction.CallbackContext context)
        {
            OnCameraLookEvent?.Invoke(context.ReadValue<Vector2>());
            
        }

        public void OnRun(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnRunEvent?.Invoke(true);
            }
            else if (context.canceled)
            {
                OnRunEvent?.Invoke(false);
            }
        
        }

        public void OnCrouch(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnCrouchEvent?.Invoke(true);
            }
            else if (context.canceled)
            {
                OnCrouchEvent?.Invoke(false);
            }
            
        }

        public void OnFlashlight(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnFlashlightEvent?.Invoke(true);
            }
            else if (context.canceled)
            {
                OnFlashlightEvent?.Invoke(false);
            }
            
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnInteractEvent?.Invoke(true);
            }
            else if (context.canceled)
            {
                OnInteractEvent?.Invoke(false);
            }
            
        }
        
        public void OnMap(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnMapEvent?.Invoke(true);
            }
            else if (context.canceled)
            {
                OnMapEvent?.Invoke(false);
            }
            
        }
    
        
        private void OnDisable()
        {
            _playerActions.Game.Disable();
            _playerActions = null;
            
        }    
        
    }
}

