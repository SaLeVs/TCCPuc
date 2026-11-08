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

        /// <summary>Creates a lobby with us as host. False when it failed; the reason is already reported.</summary>
        public async Task<bool> CreateLobbyAsync()
        {
            Unity.Services.Lobbies.Models.Lobby lobby;

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
                
                lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, MAX_PLAYERS, createLobbyOptions);
            }
            catch (Exception exception)
            {
                // A warning, not an error: the menu shows the reason, so it must not pop up as a toast too.
                Debug.LogWarning($"Lobby: could not create a lobby. {exception}");
                ConnectionFeedback.Report(DescribeServiceError(exception) ?? "Não foi possível criar a partida. Tente de novo.");
                return false;
            }

            _hostLobby = lobby;
            _joinedLobby = _hostLobby;

            OnJoinedLobby?.Invoke();
            return true;
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
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Joins the lobby with this code. False when there is no such lobby, it is full, its match
        /// already started or the service failed; the reason is already reported.
        /// </summary>
        public async Task<bool> JoinLobbyByCodeAsync(string code)
        {
            Unity.Services.Lobbies.Models.Lobby lobby;

            try
            {
                JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions
                {
                    Player = GetPlayer()
                };
                lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code, options);
            }
            catch (Exception e)
            {
                // A warning, not an error: the menu shows the reason, so it must not pop up as a toast too.
                Debug.LogWarning($"Lobby: could not join lobby '{code}'. {e}");
                ConnectionFeedback.Report(DescribeJoinError(e));
                return false;
            }

            // Its match is already running. CheckIfGameStarted would drop us straight into it halfway
            // through, so to the player this is a match that is no longer there to join.
            if (lobby.Data != null && lobby.Data.TryGetValue("StartGame", out DataObject startGame) && startGame.Value == "1")
            {
                await TryRemoveSelfAsync(lobby.Id);
                ConnectionFeedback.Report("Essa partida já começou. Peça um código novo para quem criou.");
                return false;
            }

            _joinedLobby = lobby;

            OnJoinedLobby?.Invoke();
            return true;
        }

        private static async Task TryRemoveSelfAsync(string lobbyId)
        {
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(lobbyId, AuthenticationService.Instance.PlayerId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Lobby: could not leave lobby {lobbyId}. {e.Message}");
            }
        }

        private static string DescribeJoinError(Exception exception)
        {
            if (exception is LobbyServiceException lobbyException)
            {
                switch (lobbyException.Reason)
                {
                    case LobbyExceptionReason.LobbyNotFound:
                    case LobbyExceptionReason.EntityNotFound:
                    case LobbyExceptionReason.InvalidJoinCode:
                        return "Partida não encontrada. Confira o código e tente de novo.";

                    case LobbyExceptionReason.ValidationError:
                    case LobbyExceptionReason.BadRequest:
                        return "Código inválido. Confira o código e tente de novo.";

                    case LobbyExceptionReason.LobbyFull:
                        return "Essa partida já está cheia.";

                    case LobbyExceptionReason.LobbyLocked:
                        return "Essa partida não está aceitando novos jogadores.";
                }
            }

            return DescribeServiceError(exception) ?? "Não foi possível entrar na partida. Tente de novo.";
        }

        /// <summary>Failures that are not about one lobby in particular. Null when it is none of them.</summary>
        private static string DescribeServiceError(Exception exception)
        {
            if (exception is not LobbyServiceException lobbyException) return null;

            switch (lobbyException.Reason)
            {
                case LobbyExceptionReason.RateLimited:
                    return "Muitas tentativas seguidas. Espere alguns segundos e tente de novo.";

                case LobbyExceptionReason.NetworkError:
                case LobbyExceptionReason.RequestTimeOut:
                case LobbyExceptionReason.ServiceUnavailable:
                case LobbyExceptionReason.GatewayTimeout:
                case LobbyExceptionReason.BadGateway:
                case LobbyExceptionReason.InternalServerError:
                    return "Sem resposta do serviço de partidas. Verifique sua internet e tente de novo.";

                default:
                    return null;
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
                Debug.LogException(e);
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
    
            Unity.Services.Lobbies.Models.Lobby updatedLobby = await LobbyService.Instance.UpdatePlayerAsync(_joinedLobby.Id, AuthenticationService.Instance.PlayerId, playerOptions);

            _joinedLobby = updatedLobby;
            if (IsHost()) _hostLobby = updatedLobby;

            OnLobbyUpdated?.Invoke();
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
        
        /// <summary>Starts the match for the whole lobby (host only). True once the host session is up.</summary>
        public async Task<bool> StartGame()
        {
            if (!IsHost()) return false;
            if (!AreAllPlayersReady()) return false;

            // Already started: running it again would restart the host session under everyone.
            if (_hasJoinedGame) return false;

            string joinCode = await HostSingleton.instance.gameManager.StartHostAsync();

            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogError("Lobby: host did not start, aborting StartGame.");
                return false;
            }

            // We are in the game from here on. Set this before publishing StartGame below, or the
            // next lobby poll reads our own "1" back and makes us join our own session as a client.
            _hasJoinedGame = true;

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

            return true;
        }
        
        private async void CheckIfGameStarted()
        {
            if (_hasJoinedGame) return;

            // The host already runs the session it created in StartGame(); connecting to its own
            // relay code as a client would shut that session down.
            if (IsHost()) return;

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

