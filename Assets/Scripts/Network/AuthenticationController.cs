using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

namespace Network
{
    public static class AuthenticationController
    {
        public static AuthenticationState CurrentAuthentaticationState { get; private set; } = AuthenticationState.NotAuthenticated;

        public static async Task<AuthenticationState> Authenticate(int maxTries)
        {
            if (CurrentAuthentaticationState == AuthenticationState.Authenticated)
            {
                return CurrentAuthentaticationState;
            }

            if (CurrentAuthentaticationState == AuthenticationState.Authenticating)
            {
                await Authenticating();
                return CurrentAuthentaticationState;
            }
            
            await SignInAnonymouslyAsync(maxTries);
            return CurrentAuthentaticationState;
            
        }
        
        public static async Task SignInAnonymouslyAsync(int maxTries)
        {
            CurrentAuthentaticationState = AuthenticationState.Authenticating;
            
            int triesCounter = 0;

            while (CurrentAuthentaticationState == AuthenticationState.Authenticating  && triesCounter < maxTries)
            {
                try
                {
                    // Already signed in from a sign-in that outlived its timeout: nothing to send.
                    if (!AuthenticationService.Instance.IsSignedIn)
                    {
                        await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    }

                    if(AuthenticationService.Instance.IsSignedIn && AuthenticationService.Instance.IsAuthorized)
                    {
                        CurrentAuthentaticationState = AuthenticationState.Authenticated;

                        // Voice is not part of signing in: Vivox has servers of its own, and a network
                        // that blocks only those used to fail the whole sign-in and keep the menu shut.
                        await TryInitializeVoiceAsync();
                        break;
                    }
                }
                catch (Exception ex)
                {
                    // The state is left alone here: it is what the loop tests, and writing Error into it
                    // ended the loop on the first failure, so maxTries never meant anything.
                    Debug.LogWarning($"Authentication attempt {triesCounter + 1}/{maxTries} failed: {ex.Message}");
                }

                triesCounter++;

                if (CurrentAuthentaticationState == AuthenticationState.Authenticating && triesCounter < maxTries)
                {
                    await Task.Delay(1000);
                }
            }

            if (CurrentAuthentaticationState != AuthenticationState.Authenticated)
            {
                CurrentAuthentaticationState = AuthenticationState.Timeout;
                Debug.LogWarning("Authentication did not complete: online play is off, LAN still works.");
            }

        }

        /// <summary>Called by ClientGameManager when the whole sign-in ran past its time limit.</summary>
        public static void MarkTimedOut()
        {
            if (CurrentAuthentaticationState == AuthenticationState.Authenticated) return;

            CurrentAuthentaticationState = AuthenticationState.Timeout;
        }

        private static async Task TryInitializeVoiceAsync()
        {
            try
            {
                await VivoxService.Instance.InitializeAsync();
                OnlineServices.IsVoiceAvailable = true;
            }
            catch (Exception ex)
            {
                OnlineServices.IsVoiceAvailable = false;
                Debug.LogWarning($"Vivox is unavailable, the game goes on without voice chat. {ex.Message}");
            }
        }

        private static async Task<AuthenticationState> Authenticating()
        {
            while (CurrentAuthentaticationState == AuthenticationState.Authenticating || CurrentAuthentaticationState == AuthenticationState.NotAuthenticated)
            {
                await Task.Delay(200);
            }

            return CurrentAuthentaticationState;
        }
    }

    public enum AuthenticationState
    {
        NotAuthenticated,
        Authenticating,
        Authenticated,
        Error,
        Timeout
    }
}
    

