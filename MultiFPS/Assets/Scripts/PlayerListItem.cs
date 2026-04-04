using UnityEngine;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine.UI;

public class PlayerListItem : MonoBehaviour
{
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text hostIndicatorText; // (Opsiyonel) Host yazısı
    [SerializeField] private Button kickButton; // Sadece Host'ta gözükecek buton

    private string _playerId;

    private void Start()
    {
        if (kickButton != null)
        {
            kickButton.onClick.AddListener(OnKickClicked);
        }
    }

    public void Setup(Player player, Lobby lobby)
    {
        _playerId = player.Id;

        // 1. Oyuncunun ismini belirle
        if (player.Data != null && player.Data.ContainsKey("PlayerName"))
        {
            playerNameText.text = player.Data["PlayerName"].Value;
        }
        else
        {
            playerNameText.text = "Oyuncu_" + player.Id.Substring(0, 4);
        }

        bool isThisPlayerHost = (lobby.HostId == player.Id);

        // 2. Bu oyuncu odanın Host'u mu?
        if (hostIndicatorText != null)
        {
            hostIndicatorText.text = isThisPlayerHost ? "Host" : "Oyuncu";
        }

        // 3. Kick butonu görünürlüğü
        // Sadece kendi ekranımda host isem ve bu baktığım satır hostun kendisi değilse butonu göster
        if (kickButton != null)
        {
            bool amIHost = LobbyManager.Instance.IsHost;
            kickButton.gameObject.SetActive(amIHost && !isThisPlayerHost);
        }
    }

    private async void OnKickClicked()
    {
        if (string.IsNullOrEmpty(_playerId)) return;
        
        if (kickButton != null) kickButton.interactable = false;
        
        await LobbyManager.Instance.KickPlayerAsync(_playerId);
    }
}
