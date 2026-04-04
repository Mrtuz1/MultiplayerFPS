using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    public event Action OnInitialized;
    public event Action<List<Lobby>> OnLobbyListRefreshed;
    public event Action<Lobby> OnLobbyJoined;
    public event Action<Lobby> OnLobbyUpdated;
    public event Action OnLobbyLeft;
    public event Action<string> OnError;

    private const string KEY_RELAY_CODE    = "RelayCode";
    private const string KEY_PASSWORD      = "Password";
    private const string KEY_GAME_STARTED  = "GameStarted";
    private const float  HEARTBEAT_INTERVAL = 15f;
    private const float  POLL_INTERVAL      = 1.5f;
    private const string GAME_SCENE_NAME    = "GameScene";

    public Lobby JoinedLobby { get; private set; }
    public bool IsHost => JoinedLobby != null &&
                          JoinedLobby.HostId == AuthenticationService.Instance.PlayerId;

    private Coroutine _heartbeatCoroutine;
    private Coroutine _pollCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        await InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                InitializationOptions options = new InitializationOptions();
#if UNITY_EDITOR
                options.SetProfile("Player_" + UnityEngine.Random.Range(10000, 99999));
#endif
                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignedIn += () =>
                    Debug.Log("[Auth] Signed in: " + AuthenticationService.Instance.PlayerId);
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
            Debug.Log("[LobbyManager] Ready.");
            OnInitialized?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError("[LobbyManager] Init error: " + e.Message);
            OnError?.Invoke("Servis baslatma hatasi. Baglantini kontrol et.");
        }
    }

    public async Task<bool> CreateLobbyAsync(string lobbyName, int maxPlayers, bool isPrivate, string password = "", string playerName = "")
    {
        try
        {
            var options = new CreateLobbyOptions
            {
                Player = GetPlayerObj(playerName),
                IsPrivate = isPrivate,
                Data = new Dictionary<string, DataObject>
                {
                    { KEY_PASSWORD,     new DataObject(DataObject.VisibilityOptions.Public,  password) },
                    { KEY_RELAY_CODE,   new DataObject(DataObject.VisibilityOptions.Member,  "") },
                    { KEY_GAME_STARTED, new DataObject(DataObject.VisibilityOptions.Member,  "false") }
                }
            };
            JoinedLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            Debug.Log("[Lobby] Created: " + JoinedLobby.Name + " Code: " + JoinedLobby.LobbyCode);
            _heartbeatCoroutine = StartCoroutine(HeartbeatCoroutine());
            _pollCoroutine      = StartCoroutine(LobbyPollCoroutine());
            OnLobbyJoined?.Invoke(JoinedLobby);
            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("[Lobby] Create error: " + e.Message);
            OnError?.Invoke("Lobi olusturulamadi: " + e.Message);
            return false;
        }
    }

    public async Task<List<Lobby>> ListPublicLobbiesAsync()
    {
        try
        {
            var options = new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                },
                Order = new List<QueryOrder>
                {
                    new QueryOrder(asc: false, field: QueryOrder.FieldOptions.Created)
                }
            };
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            OnLobbyListRefreshed?.Invoke(response.Results);
            return response.Results;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("[Lobby] List error: " + e.Message);
            OnError?.Invoke("Lobiler listelenemedi.");
            return new List<Lobby>();
        }
    }

    public async Task<bool> JoinLobbyByCodeAsync(string code, string password = "", string playerName = "")
    {
        try
        {
            var options = new JoinLobbyByCodeOptions { Player = GetPlayerObj(playerName) };
            JoinedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(code.Trim().ToUpper(), options);
            if (!ValidatePassword(password)) return false;
            _pollCoroutine = StartCoroutine(LobbyPollCoroutine());
            OnLobbyJoined?.Invoke(JoinedLobby);
            Debug.Log("[Lobby] Joined by code: " + JoinedLobby.Name);
            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("[Lobby] JoinByCode error: " + e.Message);
            OnError?.Invoke(e.Reason == LobbyExceptionReason.LobbyNotFound
                ? "Lobi bulunamadi. Kodu kontrol et."
                : "Girilemiyor: " + e.Message);
            return false;
        }
    }

    public async Task<bool> JoinPublicLobbyAsync(Lobby lobby, string password = "", string playerName = "")
    {
        try
        {
            var options = new JoinLobbyByIdOptions { Player = GetPlayerObj(playerName) };
            JoinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id, options);
            if (!ValidatePassword(password)) return false;
            _pollCoroutine = StartCoroutine(LobbyPollCoroutine());
            OnLobbyJoined?.Invoke(JoinedLobby);
            Debug.Log("[Lobby] Joined: " + JoinedLobby.Name);
            return true;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("[Lobby] JoinPublic error: " + e.Message);
            OnError?.Invoke("Lobiye katilanamadi: " + e.Message);
            return false;
        }
    }

    public async Task StartGameAsync()
    {
        if (!IsHost) return;
        try
        {
            string relayCode = await RelayManager.Instance.CreateRelayAndStartHost(JoinedLobby.MaxPlayers - 1);
            if (string.IsNullOrEmpty(relayCode)) { OnError?.Invoke("Relay hatasi."); return; }

            var updateOptions = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { KEY_RELAY_CODE,   new DataObject(DataObject.VisibilityOptions.Member, relayCode) },
                    { KEY_GAME_STARTED, new DataObject(DataObject.VisibilityOptions.Member, "true") }
                }
            };
            JoinedLobby = await LobbyService.Instance.UpdateLobbyAsync(JoinedLobby.Id, updateOptions);
            Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene(GAME_SCENE_NAME, LoadSceneMode.Single);
            StopHeartbeat();
            Debug.Log("[Lobby] Game started!");
        }
        catch (Exception e)
        {
            Debug.LogError("[Lobby] StartGame error: " + e.Message);
            OnError?.Invoke("Oyun baslatma hatasi.");
        }
    }

    private IEnumerator LobbyPollCoroutine()
    {
        while (JoinedLobby != null)
        {
            yield return new WaitForSeconds(POLL_INTERVAL);
            var task = LobbyService.Instance.GetLobbyAsync(JoinedLobby.Id);
            yield return new WaitUntil(() => task.IsCompleted);
            if (task.IsFaulted)
            {
                if (task.Exception?.InnerException is LobbyServiceException lobbyEx)
                {
                    if (lobbyEx.Reason == LobbyExceptionReason.LobbyNotFound || lobbyEx.Reason == LobbyExceptionReason.Forbidden)
                    {
                        JoinedLobby = null;
                        OnError?.Invoke("Oda silindi veya atildiniz.");
                        OnLobbyLeft?.Invoke();
                        break;
                    }
                }
                continue;
            }
            JoinedLobby = task.Result;
            
            bool isStillMember = false;
            string myId = AuthenticationService.Instance.PlayerId;
            foreach (var p in JoinedLobby.Players)
                if (p.Id == myId) { isStillMember = true; break; }

            if (!isStillMember)
            {
                JoinedLobby = null;
                OnError?.Invoke("Odadan atildiniz.");
                OnLobbyLeft?.Invoke();
                break;
            }

            OnLobbyUpdated?.Invoke(JoinedLobby);
            if (!IsHost)
            {
                string relayCode   = JoinedLobby.Data != null && JoinedLobby.Data.ContainsKey(KEY_RELAY_CODE)   ? JoinedLobby.Data[KEY_RELAY_CODE].Value   : "";
                string gameStarted = JoinedLobby.Data != null && JoinedLobby.Data.ContainsKey(KEY_GAME_STARTED) ? JoinedLobby.Data[KEY_GAME_STARTED].Value : "false";
                if (gameStarted == "true" && !string.IsNullOrEmpty(relayCode))
                {
                    StopPoll();
                    var joinTask = RelayManager.Instance.JoinRelayAndStartClient(relayCode);
                    yield return new WaitUntil(() => joinTask.IsCompleted);
                    Debug.Log("[Lobby] Client joined Relay.");
                    break;
                }
            }
        }
    }

    private IEnumerator HeartbeatCoroutine()
    {
        while (JoinedLobby != null && IsHost)
        {
            var task = LobbyService.Instance.SendHeartbeatPingAsync(JoinedLobby.Id);
            yield return new WaitUntil(() => task.IsCompleted);
            yield return new WaitForSeconds(HEARTBEAT_INTERVAL);
        }
    }

    public async Task LeaveOrDeleteLobbyAsync()
    {
        if (JoinedLobby == null) return;
        StopHeartbeat();
        StopPoll();
        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            if (IsHost)
                await LobbyService.Instance.DeleteLobbyAsync(JoinedLobby.Id);
            else
                await LobbyService.Instance.RemovePlayerAsync(JoinedLobby.Id, playerId);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("[Lobby] Leave error: " + e.Message);
        }
        finally
        {
            JoinedLobby = null;
            OnLobbyLeft?.Invoke();
            Debug.Log("[Lobby] Left lobby.");
        }
    }

    public async Task KickPlayerAsync(string playerId)
    {
        if (!IsHost || JoinedLobby == null) return;
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(JoinedLobby.Id, playerId);
            Debug.Log("[Lobby] Kicked player: " + playerId);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("[Lobby] Kick error: " + e.Message);
            OnError?.Invoke("Oyuncu atilamadi.");
        }
    }


    private bool ValidatePassword(string enteredPassword)
    {
        string stored = JoinedLobby?.Data?[KEY_PASSWORD]?.Value ?? "";
        if (!string.IsNullOrEmpty(stored) && stored != enteredPassword)
        {
            _ = LobbyService.Instance.RemovePlayerAsync(JoinedLobby.Id, AuthenticationService.Instance.PlayerId);
            JoinedLobby = null;
            OnError?.Invoke("Yanlis sifre!");
            return false;
        }
        return true;
    }

    private Player GetPlayerObj(string playerName)
    {
        if (string.IsNullOrEmpty(playerName)) playerName = "Oyuncu_" + UnityEngine.Random.Range(10, 99);
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
            }
        };
    }

    private void StopHeartbeat()
    {
        if (_heartbeatCoroutine != null) StopCoroutine(_heartbeatCoroutine);
        _heartbeatCoroutine = null;
    }

    private void StopPoll()
    {
        if (_pollCoroutine != null) StopCoroutine(_pollCoroutine);
        _pollCoroutine = null;
    }

    private void OnDestroy()
    {
        if (JoinedLobby != null)
            _ = LobbyService.Instance.RemovePlayerAsync(JoinedLobby.Id, AuthenticationService.Instance.PlayerId);
    }
}