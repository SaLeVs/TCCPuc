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
        public event Action OnFlashlightEvent;
        public event Action OnInteractEvent;
        public event Action OnUseEvent;
        public event Action<bool> OnMapEvent;
        public event Action<int> OnSlotEvent;
        public event Action OnPauseEvent;
        public event Action OnChatEvent;
        public event Action OnHideMissionsEvent;
        public event Action OnHideDonateEvent;

        /// <summary>Whether the crouch key is down right now, for when the last event can't be trusted.</summary>
        public bool IsCrouchHeld => _playerActions != null && _playerActions.Game.Crouch.IsPressed();


        /// <summary>
        /// The interact binding in the pieces a HUD needs to draw it: the text to print ("E"), plus
        /// the device and the control behind it ("Mouse", "leftButton") for the ones shown as an icon.
        /// </summary>
        public string GetInteractBindingDisplay(out string deviceLayout, out string controlPath) =>
            GetBindingDisplay(_playerActions?.Game.Interact, out deviceLayout, out controlPath);

        /// <summary>The key that pauses, and also closes whatever screen is open ("Esc"), in the same pieces.</summary>
        public string GetPauseBindingDisplay(out string deviceLayout, out string controlPath) =>
            GetBindingDisplay(_playerActions?.Game.Pause, out deviceLayout, out controlPath);

        /// <summary>
        /// One binding rather than all of them joined: the first that is a control of its own, which
        /// is the one the HUD has room for. Read off the action, so it follows an override.
        /// </summary>
        private static string GetBindingDisplay(InputAction action, out string deviceLayout, out string controlPath)
        {
            deviceLayout = controlPath = string.Empty;
            if (action == null) return string.Empty;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].isComposite || action.bindings[i].isPartOfComposite) continue;

                string display = action.GetBindingDisplayString(i, out deviceLayout, out controlPath,
                    InputBinding.DisplayStringOptions.DontIncludeInteractions);

                deviceLayout ??= string.Empty;
                controlPath ??= string.Empty;
                return display;
            }

            return string.Empty;
        }

        private void OnEnable()
        {
            if (_playerActions == null)
            {
                _playerActions = new PlayerActions();
                _playerActions.Game.SetCallbacks(this);
            }

            _playerActions.Game.Enable();

        }

        /// <summary>
        /// Everything the player does in the world, on or off in one place - Pause excepted, since
        /// that is the key that gets back out of the menu.
        ///
        /// <para>The whole Game map used to stay live behind the pause menu: walking, sprinting,
        /// crouching, the flashlight, the slots, interacting and the chat all still answered the
        /// keyboard with the menu up. Only the camera was held back, by its own axis controller.</para>
        ///
        /// <para>A key held down when the menu opens is the other half of it: the action keeps the
        /// value it last reported, so the body carried on walking. Movement and sprint are told to
        /// settle here, which is why this is the one call that closes both holes.</para>
        /// </summary>
        public void SetGameplayInputEnabled(bool isEnabled)
        {
            if (_playerActions == null) return;

            if (isEnabled)
            {
                _playerActions.Game.Enable();
                return;
            }

            _playerActions.Game.Disable();
            _playerActions.Game.Pause.Enable();

            OnMoveEvent?.Invoke(Vector2.zero);
            OnRunEvent?.Invoke(false);
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
                OnFlashlightEvent?.Invoke();
            }
            
        }

        public void OnInteract(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnInteractEvent?.Invoke();
            }
            
        }
        
        public void OnUse(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnUseEvent?.Invoke();
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
        
        public void OnSlot1(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnSlotEvent?.Invoke(1);
            }
        }

        public void OnSlot2(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnSlotEvent?.Invoke(2);
            }
        }

        public void OnSlot3(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnSlotEvent?.Invoke(3);
            }
        }

        public void OnSlot4(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnSlotEvent?.Invoke(4);
            }
        }

        public void OnPause(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnPauseEvent?.Invoke();
            }
                
        }

        public void OnChat(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnChatEvent?.Invoke();
            }
        }

        public void OnHideMissions(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnHideMissionsEvent?.Invoke();
            }
        }

        public void OnHideDonate(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                OnHideDonateEvent?.Invoke();
            }
        }

        private void OnDisable()
        {
            _playerActions.Game.Disable();
            _playerActions = null;
            
        }    
        
    }
}

