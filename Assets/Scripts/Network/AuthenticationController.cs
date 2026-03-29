using System.Threading.Tasks;
using Unity.Services.Authentication;
using UnityEngine;

namespace Network
{
    public static class AuthenticationController
    {
        public static AuthenticationState CurrentAuthentaticationState { get; private set; } = AuthenticationState.NotAuthenticated;

        public static async Task<AuthenticationState> Authenticate(int maxTries = 5)
        {
            if (CurrentAuthentaticationState == AuthenticationState.Authenticated)
            {
                return CurrentAuthentaticationState;
            }

            CurrentAuthentaticationState = AuthenticationState.Authenticating;
            
            int triesCounter = 0;

            while (CurrentAuthentaticationState == AuthenticationState.Authenticating  && triesCounter < maxTries)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                
                if(AuthenticationService.Instance.IsSignedIn && AuthenticationService.Instance.IsAuthorized)
                {
                    CurrentAuthentaticationState = AuthenticationState.Authenticated;
                    break;
                }
                
                triesCounter++;
                
                await Task.Delay(1000);
                
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
    

