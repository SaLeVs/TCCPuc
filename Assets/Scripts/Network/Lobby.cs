using System;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Network
{
    public class Lobby : MonoBehaviour
    {
        private const int MAX_PLAYERS = 4;
        
        private async void Start()
        {
            await UnityServices.InitializeAsync();
            AuthenticationService.Instance.SignedIn += () =>
            {
                Debug.Log("Signed in! PlayerID: " + AuthenticationService.Instance.PlayerId);
            };
            
            await AuthenticationService.Instance.SignInAnonymouslyAsync(); // Change this for steam after
            
            
        }
        
        public async void CreateLobby()
        {
            try
            {
                string lobbyName = "Lobby";
                Unity.Services.Lobbies.Models.Lobby lobby =
                    await LobbyService.Instance.CreateLobbyAsync(lobbyName, MAX_PLAYERS);
            }
            catch (LobbyServiceException exception)
            {
                Debug.Log(exception);
            }
        }
        
    }
}

