using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
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
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                
                    if(AuthenticationService.Instance.IsSignedIn && AuthenticationService.Instance.IsAuthorized)
                    {
                        CurrentAuthentaticationState = AuthenticationState.Authenticated;
                        break;
                    }
                }
                catch (AuthenticationException ex)
                {
                    CurrentAuthentaticationState = AuthenticationState.Error;
                    Debug.LogError($"Authentication error: {ex}");
                }
                catch (RequestFailedException ex)
                {
                    CurrentAuthentaticationState = AuthenticationState.Error;
                    Debug.LogError($"Request failed error: {ex}");
                }
                
                triesCounter++;
                
                await Task.Delay(1000);
            }

            if (CurrentAuthentaticationState != AuthenticationState.Authenticated)
            {
                CurrentAuthentaticationState = AuthenticationState.Timeout;
                Debug.LogError($"Authentication timed out");
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
    

