using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Network
{
    public class Lobby : MonoBehaviour
    {
        public event Action OnJoinedLobby;
        public event Action OnLobbyUpdated;
        
        private const int MAX_PLAYERS = 4;
        private const string PLAYER_READY = "Ready";
        
        public Unity.Services.Lobbies.Models.Lobby JoinedLobby => _joinedLobby;
        
        private Unity.Services.Lobbies.Models.Lobby _hostLobby;
        private Unity.Services.Lobbies.Models.Lobby _joinedLobby;
        
        private float _heartBeatTimer;
        private float _heartBeatMaxTimer = 15f;
        
        private float _lobbyUpdateTimer;
        private float _lobbyUpdateMaxTimer = 1.1f;

        private string _playerName;
        private bool _hasJoinedGame;


        private void Start()
        {
            _playerName = $"User {UnityEngine.Random.Range(0, 100)}";
        }

        private void Update()
        {
            HeartBeat();
            LobbyPullForUpdate();
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

        private async void LobbyPullForUpdate()
        {
            if (_joinedLobby != null)
            {
                _lobbyUpdateTimer -= Time.deltaTime;
                
                if (_lobbyUpdateTimer <= 0f)
                {
                    _lobbyUpdateTimer = _lobbyUpdateMaxTimer;
                    
                    Unity.Services.Lobbies.Models.Lobby lobby = await LobbyService.Instance.GetLobbyAsync(_joinedLobby.Id);
                    _joinedLobby =  lobby;
                    
                    OnLobbyUpdated?.Invoke();
                    CheckIfGameStarted();
                }
                
            }
        }

        public async Task CreateLobbyAsync()
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
                            
                        },
                        {
                            "StartGame", new DataObject(DataObject.VisibilityOptions.Member, "0")
                        }
                    }
                };
                
                string lobbyName = "Lobby";
                
                Unity.Services.Lobbies.Models.Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, MAX_PLAYERS, createLobbyOptions);
                
                _hostLobby = lobby;
                _joinedLobby = _hostLobby;

                OnJoinedLobby?.Invoke();
                
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
                Debug.Log(e);
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
                Unity.Services.Lobbies.Models.Lobby lobby  = await LobbyService.Instance.JoinLobbyByCodeAsync(code, options);
                _joinedLobby = lobby;
                
                OnJoinedLobby?.Invoke();

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
                await LobbyService.Instance.RemovePlayerAsync(_joinedLobby.Id, AuthenticationService.Instance.PlayerId);
                _joinedLobby = null;
                _hostLobby = null;
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
           
        }

        public async void KickPlayer()
        {
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(_joinedLobby.Id, _hostLobby.Players[1].Id);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }

        public async void MigrateHost()
        {
            try
            {
                _hostLobby = await LobbyService.Instance.UpdateLobbyAsync(_hostLobby.Id, new UpdateLobbyOptions
                {
                    HostId = _joinedLobby.Players[1].Id
                });
                _joinedLobby = _hostLobby;
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }

        public async void DeleteLobby()
        {
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(_joinedLobby.Id);
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
            
        }
        
        public async Task SetPlayerReady(bool isReady)
        {
            UpdatePlayerOptions playerOptions = new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    {
                        PLAYER_READY, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, isReady ? "1" : "0")
                    }
                }
            };
            
            await LobbyService.Instance.UpdatePlayerAsync(_joinedLobby.Id, AuthenticationService.Instance.PlayerId, playerOptions);
        }
        
        public bool AreAllPlayersReady()
        {
            foreach (Player player in _joinedLobby.Players)
            {
                if (!player.Data.ContainsKey(PLAYER_READY))
                {
                    return false; 
                }


                if (player.Data[PLAYER_READY].Value != "1")
                {
                    return false;
                }
                    
            }

            return true;
        }
        
        public async Task StartGame()
        {
            if (!IsHost()) return;
            if (!AreAllPlayersReady()) return;
            
            string joinCode = await HostSingleton.instance.gameManager.StartHostAsync();
            
            await LobbyService.Instance.UpdateLobbyAsync(_joinedLobby.Id,
                new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        {
                            "StartGame", new DataObject(DataObject.VisibilityOptions.Member, "1")
                        },
                        {
                            "RelayCode", new DataObject(DataObject.VisibilityOptions.Member, joinCode) 
                        }
                    }
                });
        } 
        
        private async void CheckIfGameStarted()
        {
            if (_hasJoinedGame) return;
            
            if (!_joinedLobby.Data.ContainsKey("StartGame")) return;

            if (_joinedLobby.Data["StartGame"].Value == "1")
            {
                _hasJoinedGame = true;
                string relayCode = _joinedLobby.Data["RelayCode"].Value;

                await ClientSingleton.instance.gameManager.StartClientAsync(relayCode);
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
        
        public bool IsHost()
        {
            return _joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
        }
    }
}

