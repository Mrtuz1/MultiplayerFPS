using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NetworkManagerUI : MonoBehaviour
{
    [SerializeField] private Button hostBtn;
    [SerializeField] private Button clientBtn;

    private void Awake()
    {
        hostBtn.onClick.AddListener(() => {
            NetworkManager.Singleton.StartHost();

            // Host olarak baþlar baþlamaz herkesi GameScene'e sürüklüyoruz.
            // LoadSceneMode.Single -> Önceki sahneyi kapat, sadece bu sahneyi aç demek.
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        });

        clientBtn.onClick.AddListener(() => {
            NetworkManager.Singleton.StartClient();
            // Client'ýn sahne yükleme kodu yazmasýna gerek YOKTUR. 
            // NGO, Client'ý otomatik olarak Host'un bulunduðu sahneye çeker.
        });
    }
}