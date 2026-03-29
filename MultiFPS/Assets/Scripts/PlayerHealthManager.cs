using Unity.Netcode;
using UnityEngine;
using TMPro; // TextMeshPro kullanýyorsan bunu ekle, normal Text ise UnityEngine.UI ekle

public class PlayerHealthManager : NetworkBehaviour
{
    [Header("UI Referanslarý")]
    public TextMeshProUGUI healthText; // Canvas'taki can yazýmýz

    // Senin yazdýðýn o kusursuz deðiþken
    public NetworkVariable<int> playerHealth = new NetworkVariable<int>(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        // Obje aðda doðduðunda, can deðiþkeninin "Deðiþme Olayýna" abone oluyoruz (Subscribe)
        playerHealth.OnValueChanged += OnHealthChanged;

        // Oyuna ilk girdiðimizde canýmýz 100 yazsýn diye baþlangýç güncellemesi
        if (IsOwner && healthText != null)
        {
            healthText.text = playerHealth.Value.ToString();
        }
    }

    public override void OnNetworkDespawn()
    {
        // Obje silinirken aboneliði iptal et (Memory leak / bellek sýzýntýsý olmasýn diye kuraldýr)
        playerHealth.OnValueChanged -= OnHealthChanged;
    }

    // SERVER'IN ÇAÐIRACAÐI FONKSÝYON (RPC DEÐÝL DÝREKT METOT)
    public void TakeDamage(int damage)
    {
        // Güvenlik: Eðer bu kodu Server dýþýnda biri çalýþtýrmaya kalkarsa reddet
        if (!IsServer) return;

        // Server acýmaz, caný direkt düþürür
        playerHealth.Value -= damage;
    }

    // SÝHÝRLÝ FONKSÝYON: Can her deðiþtiðinde HERKESTE otomatik tetiklenir
    private void OnHealthChanged(int previousValue, int newValue)
    {
        // Sadece kendi karakterimse ekranýmdaki UI'ý güncelle
        if (IsOwner && healthText != null)
        {
            healthText.text = newValue.ToString();
        }
    }
}