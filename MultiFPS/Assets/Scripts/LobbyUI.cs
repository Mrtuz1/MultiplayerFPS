using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    public static LobbyUI Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject createLobbyPanel;
    [SerializeField] private GameObject lobbyListPanel;
    [SerializeField] private GameObject joinByCodePanel;
    [SerializeField] private GameObject lobbyRoomPanel;
    [SerializeField] private GameObject loadingPanel;

    [Header("Main Menu")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private Button         createLobbyBtn;
    [SerializeField] private Button         lobbyListBtn;
    [SerializeField] private Button         joinByCodeBtn;

    [Header("Create Lobby")]
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private TMP_Dropdown   maxPlayersDropdown;
    [SerializeField] private Toggle         privateToggle;
    [SerializeField] private GameObject     passwordCreateSection;
    [SerializeField] private TMP_InputField createPasswordInput;
    [SerializeField] private Button         confirmCreateBtn;
    [SerializeField] private Button         backFromCreateBtn;

    [Header("Lobby List")]
    [SerializeField] private Transform      lobbyListContent;
    [SerializeField] private GameObject     lobbyListItemPrefab;
    [SerializeField] private Button         refreshListBtn;
    [SerializeField] private Button         backFromListBtn;

    [Header("Join By Code")]
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private TMP_InputField codePasswordInput;
    [SerializeField] private Button         confirmJoinCodeBtn;
    [SerializeField] private Button         backFromCodeBtn;

    [Header("Lobby Room")]
    [SerializeField] private TMP_Text   roomNameText;
    [SerializeField] private TMP_Text   roomCodeText;
    [SerializeField] private Transform  playerListContent;
    [SerializeField] private GameObject playerListItemPrefab;
    [SerializeField] private Button     startGameBtn;
    [SerializeField] private Button     leaveRoomBtn;

    [Header("Feedback")]
    [SerializeField] private TMP_Text errorText;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        LobbyManager.Instance.OnInitialized       += OnServiceReady;
        LobbyManager.Instance.OnLobbyListRefreshed += RefreshLobbyListUI;
        LobbyManager.Instance.OnLobbyJoined       += OnJoinedLobby;
        LobbyManager.Instance.OnLobbyUpdated      += OnLobbyUpdated;
        LobbyManager.Instance.OnLobbyLeft         += OnLobbyLeft;
        LobbyManager.Instance.OnError             += ShowError;

        createLobbyBtn.onClick.AddListener(() => ShowPanel(createLobbyPanel));
        lobbyListBtn.onClick.AddListener(OnClickLobbyList);
        joinByCodeBtn.onClick.AddListener(() => ShowPanel(joinByCodePanel));

        privateToggle.onValueChanged.AddListener(isOn => passwordCreateSection.SetActive(isOn));
        confirmCreateBtn.onClick.AddListener(OnClickCreateLobby);
        backFromCreateBtn.onClick.AddListener(() => ShowPanel(mainMenuPanel));

        refreshListBtn.onClick.AddListener(OnClickRefreshList);
        backFromListBtn.onClick.AddListener(() => ShowPanel(mainMenuPanel));

        confirmJoinCodeBtn.onClick.AddListener(OnClickJoinByCode);
        backFromCodeBtn.onClick.AddListener(() => ShowPanel(mainMenuPanel));

        startGameBtn.onClick.AddListener(OnClickStartGame);
        leaveRoomBtn.onClick.AddListener(OnClickLeaveRoom);


        maxPlayersDropdown.ClearOptions();
        maxPlayersDropdown.AddOptions(new List<string> { "2", "4", "6", "8" });
        maxPlayersDropdown.value = 3;

        ShowPanel(loadingPanel);
        HideError();
        passwordCreateSection.SetActive(false);
    }

    private void OnServiceReady() => ShowPanel(mainMenuPanel);

    private async void OnClickCreateLobby()
    {
        string lobbyName = lobbyNameInput.text.Trim();
        if (string.IsNullOrEmpty(lobbyName)) lobbyName = "Oda_" + Random.Range(100, 999);
        int maxPlayers = int.Parse(maxPlayersDropdown.options[maxPlayersDropdown.value].text);
        bool isPrivate = privateToggle.isOn;
        string password = isPrivate ? createPasswordInput.text : "";
        ShowLoading(true);
        await LobbyManager.Instance.CreateLobbyAsync(lobbyName, maxPlayers, isPrivate, password, playerNameInput.text);
        ShowLoading(false);
    }

    private async void OnClickLobbyList()
    {
        ShowPanel(lobbyListPanel);
        ShowLoading(true);
        await LobbyManager.Instance.ListPublicLobbiesAsync();
        ShowLoading(false);
    }

    private async void OnClickRefreshList()
    {
        ShowLoading(true);
        await LobbyManager.Instance.ListPublicLobbiesAsync();
        ShowLoading(false);
    }

    private void RefreshLobbyListUI(List<Lobby> lobbies)
    {
        foreach (Transform child in lobbyListContent)
            Destroy(child.gameObject);

        foreach (Lobby lobby in lobbies)
        {
            var item   = Instantiate(lobbyListItemPrefab, lobbyListContent);
            var itemUI = item.GetComponent<LobbyListItem>();
            if (itemUI != null) itemUI.Setup(lobby, OnLobbyItemClicked);
        }
    }

    private void OnLobbyItemClicked(Lobby lobby)
    {
        JoinPublicLobby(lobby, "");
    }

    private async void JoinPublicLobby(Lobby lobby, string password)
    {
        ShowLoading(true);
        await LobbyManager.Instance.JoinPublicLobbyAsync(lobby, password, playerNameInput.text);
        ShowLoading(false);
    }

    private async void OnClickJoinByCode()
    {
        string code = codeInput.text.Trim();
        if (string.IsNullOrEmpty(code)) { ShowError("Kod bos olamaz!"); return; }
        ShowLoading(true);
        await LobbyManager.Instance.JoinLobbyByCodeAsync(code, codePasswordInput.text, playerNameInput.text);
        ShowLoading(false);
    }

    private void OnJoinedLobby(Lobby lobby)
    {
        ShowPanel(lobbyRoomPanel);
        UpdateRoomPanel(lobby);
    }

    private void OnLobbyUpdated(Lobby lobby)
    {
        if (lobbyRoomPanel.activeSelf) UpdateRoomPanel(lobby);
    }

    private void UpdateRoomPanel(Lobby lobby)
    {
        roomNameText.text = lobby.Name;
        roomCodeText.text = "Lobi Kodu: " + lobby.LobbyCode;

        foreach (Transform child in playerListContent)
            Destroy(child.gameObject);

        foreach (Player player in lobby.Players)
        {
            var item = Instantiate(playerListItemPrefab, playerListContent);
            var playerItem = item.GetComponent<PlayerListItem>();
            if (playerItem != null)
            {
                playerItem.Setup(player, lobby);
            }
            else
            {
                var txt = item.GetComponentInChildren<TMP_Text>();
                if (txt) txt.text = player.Id == lobby.HostId ? "Host" : "Oyuncu";
            }
        }

        bool amHost = LobbyManager.Instance.IsHost;
        startGameBtn.gameObject.SetActive(amHost);
        roomCodeText.gameObject.SetActive(amHost);
    }

    private async void OnClickStartGame()
    {
        ShowLoading(true);
        await LobbyManager.Instance.StartGameAsync();
        ShowLoading(false);
    }

    private async void OnClickLeaveRoom()
    {
        ShowLoading(true);
        await LobbyManager.Instance.LeaveOrDeleteLobbyAsync();
        ShowLoading(false);
    }

    private void OnLobbyLeft() => ShowPanel(mainMenuPanel);

    private void ShowPanel(GameObject panel)
    {
        mainMenuPanel.SetActive(panel == mainMenuPanel);
        createLobbyPanel.SetActive(panel == createLobbyPanel);
        lobbyListPanel.SetActive(panel == lobbyListPanel);
        joinByCodePanel.SetActive(panel == joinByCodePanel);
        lobbyRoomPanel.SetActive(panel == lobbyRoomPanel);
        loadingPanel.SetActive(panel == loadingPanel);
        HideError();
    }

    private void ShowLoading(bool show)
    {
        loadingPanel.SetActive(show);
    }

    public void ShowError(string message)
    {
        if (errorText == null) return;
        errorText.text = message;
        errorText.gameObject.SetActive(true);
        CancelInvoke(nameof(HideError));
        Invoke(nameof(HideError), 4f);
    }

    private void HideError()
    {
        if (errorText != null) errorText.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (LobbyManager.Instance == null) return;
        LobbyManager.Instance.OnInitialized       -= OnServiceReady;
        LobbyManager.Instance.OnLobbyListRefreshed -= RefreshLobbyListUI;
        LobbyManager.Instance.OnLobbyJoined       -= OnJoinedLobby;
        LobbyManager.Instance.OnLobbyUpdated      -= OnLobbyUpdated;
        LobbyManager.Instance.OnLobbyLeft         -= OnLobbyLeft;
        LobbyManager.Instance.OnError             -= ShowError;
    }
}