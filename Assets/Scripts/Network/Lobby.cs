using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Systems;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Network
{
    public class Lobby : MonoBehaviour
    {
        private static Lobby Instance;

        public static Lobby instance
        {
            get
            {
                if (Instance != null) return Instance;
                Instance = FindFirstObjectByType<Lobby>();
                if (Instance == null)
                {
                    Debug.LogError("Lobby not found");
                    return null;
                }
                return Instance;
            }
        }
        
        public event Action OnJoinedLobby;
        public event Action OnLobbyUpdated;
        public event Action OnLeftLobby;
        
        private const int MAX_PLAYERS = 4;
        private const string PLAYER_READY = "Ready";
        
        public Unity.Services.Lobbies.Models.Lobby JoinedLobby => _joinedLobby;
        
        private Unity.Services.Lobbies.Models.Lobby _hostLobby;
        private Unity.Services.Lobbies.Models.Lobby _joinedLobby;
        
        private float _heartBeatTimer;
        private float _heartBeatMaxTimer = 15f;
        
        private float _lobbyUpdateTimer;
        private float _lobbyUpdateMaxTimer = 1.1f;
        
        private bool _hasJoinedGame;
        

        private void Update()
        {
            HeartBeat();
            LobbyPullForUpdate();
        }

        private async void HeartBeat()
        {
            if (_hostLobby == null) return;
            if (_hasJoinedGame) return; 

            _heartBeatTimer -= Time.deltaTime;

            if (_heartBeatTimer <= 0f)
            {
                _heartBeatTimer = _heartBeatMaxTimer;
                try
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(_hostLobby.Id);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Heartbeat failed: {e.Message}");
                }
            }
        }

        private async void LobbyPullForUpdate()
        {
            if (_joinedLobby == null) return;
            if (_hasJoinedGame) return;  

            _lobbyUpdateTimer -= Time.deltaTime;

            if (_lobbyUpdateTimer <= 0f)
            {
                _lobbyUpdateTimer = _lobbyUpdateMaxTimer;

                try
                {
                    Unity.Services.Lobbies.Models.Lobby lobby = await LobbyService.Instance.GetLobbyAsync(_joinedLobby.Id);
                    _joinedLobby = lobby;

                    OnLobbyUpdated?.Invoke();
                    CheckIfGameStarted();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Lobby poll failed: {e.Message}");
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
                if (_joinedLobby == null) return;

                if (IsHost())
                {
                    if (_joinedLobby.Players.Count > 1)
                    {
                        await MigrateHostAsync();
                        await LobbyService.Instance.RemovePlayerAsync(_joinedLobby.Id, AuthenticationService.Instance.PlayerId);
                    }
                    else
                    {
                        await LobbyService.Instance.DeleteLobbyAsync(_joinedLobby.Id);
                    }
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(_joinedLobby.Id, AuthenticationService.Instance.PlayerId);
                }
            }
            finally
            {
                _joinedLobby = null;
                _hostLobby = null;
                _hasJoinedGame = false;
                ResetLobbyState();
                OnLeftLobby?.Invoke();
            }
            
        }

        private async Task MigrateHostAsync()
        {
            string myId = AuthenticationService.Instance.PlayerId;
            Player nextHost = _joinedLobby.Players.Find(p => p.Id != myId);

            if (nextHost == null) return;

            _hostLobby = await LobbyService.Instance.UpdateLobbyAsync(_joinedLobby.Id, new UpdateLobbyOptions
            {
                HostId = nextHost.Id
            });
            _joinedLobby = _hostLobby;
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
            PlayerTracker.Instance.SetExpectedPlayerCount(_joinedLobby.Players.Count);
            
            
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
        
        public void ResetLobbyState()
        {
            _hasJoinedGame = false;

            _joinedLobby = null;
            _hostLobby = null;

            _heartBeatTimer = 0f;
            _lobbyUpdateTimer = 0f;
        }
        
        private Player GetPlayer()
        {
            UserData userData = LocalUserData.Load();
            userData.userAuthId = AuthenticationService.Instance.PlayerId;
    
            return new Player
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "UserData", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, JsonUtility.ToJson(userData)) },
                    { PLAYER_READY, new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "0") }
                }
            };
        }
        
        public bool IsHost()
        {
            return _joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
        }
    }
}

