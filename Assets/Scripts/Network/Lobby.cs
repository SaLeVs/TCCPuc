using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Network
{
    public class Lobby : MonoBehaviour
    {
        private const int MAX_PLAYERS = 4;
        private Unity.Services.Lobbies.Models.Lobby _hostLobby;
        
        private float _heartBeatTimer;
        private float _heartBeatMaxTimer = 15f;

        private string _playerName;
        
        
        private async void Start()
        {
            await UnityServices.InitializeAsync();
            AuthenticationService.Instance.SignedIn += () =>
            {
                Debug.Log("Signed in! PlayerID: " + AuthenticationService.Instance.PlayerId);
            };

            _playerName = $"User {UnityEngine.Random.Range(0, 100)}";
            await AuthenticationService.Instance.SignInAnonymouslyAsync(); // Change this for steam after
            
        }

        private void Update()
        {
            HeartBeat();
        }

        private async void HeartBeat()
        {
            if (_hostLobby != null)
            {
                _heartBeatTimer -= Time.deltaTime;
                
                if (_heartBeatTimer <= 0f)
                {
                    _heartBeatTimer = _heartBeatMaxTimer;
                    await LobbyService.Instance.SendHeartbeatPingAsync(_hostLobby.Id);
                    
                }
                
            }
        }

        public async void CreateLobby()
        {
            try
            {
                CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Player = GetPlayer(),
                    Data = new Dictionary<string, DataObject>
                    {
                        {
                            "GameMode", new DataObject(DataObject.VisibilityOptions.Public, "Survival")
                        }
                    }
                };
                
                string lobbyName = "Lobby";
                
                Unity.Services.Lobbies.Models.Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, MAX_PLAYERS, createLobbyOptions);
                _hostLobby = lobby;
                
                Debug.Log($"Lobby created:{lobby}");
                
            }
            catch (LobbyServiceException exception)
            {
                Debug.Log(exception);
            }
        }

        public async void ListLobbies()
        {
            try
            {
                QueryLobbiesOptions options = new QueryLobbiesOptions
                {
                    Count = 25,
                    Filters = new List<QueryFilter>
                    {
                        new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)

                    },
                    Order = new List<QueryOrder>
                    {
                        new QueryOrder(false, QueryOrder.FieldOptions.Created)
                    }
                };
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async void JoinLobbyByCode(string code)
        {
            try
            {
                JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
                {
                    Player = GetPlayer()
                };
                
                await LobbyService.Instance.JoinLobbyByCodeAsync(code, options);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            
        }

        public async void LeaveLobby()
        {
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(_hostLobby.Id, AuthenticationService.Instance.PlayerId);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
           
        }

        private async void KickPlayer()
        {
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(_hostLobby.Id, _hostLobby.Players[1].Id);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }

        private Player GetPlayer()
        {
            return new Player
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    {"PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, _playerName)}
                }
            };
        }
    }
}

