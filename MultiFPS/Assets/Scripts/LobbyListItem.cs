using System;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lobi listesindeki tek bir satirin (prefab) controller'i.
/// </summary>
public class LobbyListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text lobbyNameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private GameObject lockIcon;
    [SerializeField] private Button joinButton;

    private Lobby _lobby;
    private Action<Lobby> _onClickCallback;

    public void Setup(Lobby lobby, Action<Lobby> onClickCallback)
    {
        _lobby           = lobby;
        _onClickCallback = onClickCallback;

        lobbyNameText.text   = lobby.Name;
        playerCountText.text = lobby.Players.Count + "/" + lobby.MaxPlayers;

        string password = lobby.Data?["Password"]?.Value ?? "";
        if (lockIcon != null)
            lockIcon.SetActive(!string.IsNullOrEmpty(password));

        joinButton.onClick.AddListener(() => _onClickCallback?.Invoke(_lobby));
    }
}
