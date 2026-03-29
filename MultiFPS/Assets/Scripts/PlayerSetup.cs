using Unity.Netcode;
using UnityEngine;

public class PlayerSetup : NetworkBehaviour
{
    private Camera playerCamera;
    private AudioListener audioListener;
    [SerializeField] private GameObject playerCanvas; // Yeni ekledik, Canvas objesini buraya atacaðýz

    private void Awake()
    {
        playerCamera = GetComponentInChildren<Camera>();
        audioListener = GetComponentInChildren<AudioListener>();
    }

    public override void OnNetworkSpawn()
    {
        // 1. Kamera ve Ses Ayarlarý (Sadece Kendi Ekranýmýz)
        if (!IsOwner)
        {
            if (playerCamera != null) playerCamera.enabled = false;
            if (audioListener != null) audioListener.enabled = false;
            if (playerCanvas != null) playerCanvas.SetActive(false); // Düþmanýn UI'ýný görmeyelim
        }

        // 2. Doðma Noktasý Ayarlama (SADECE SERVER YETKÝLÝ)
        if (IsServer)
        {
            SetRandomSpawnPoint();
        }
    }

    private void SetRandomSpawnPoint()
    {
        // Sahnede "SpawnPoint" etiketli tüm objeleri bir diziye (array) alýyoruz
        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");

        if (spawnPoints.Length > 0)
        {
            // Rastgele bir nokta seçip oyuncuyu oraya yerleþtiriyoruz
            int randomIndex = Random.Range(0, spawnPoints.Length);
            transform.position = spawnPoints[randomIndex].transform.position;
            transform.rotation = spawnPoints[randomIndex].transform.rotation;
        }
    }
}