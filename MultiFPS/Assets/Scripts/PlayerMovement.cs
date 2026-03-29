using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Referanslar")]
    public CharacterController controller; // Sahnede prefab'a eklediðin component
    public Transform cameraTransform;      // Oyuncunun içindeki kamera

    [Header("Ayarlar")]
    public float speed = 6f;               // Yürüme hýzý
    public float mouseSensitivity = 200f;  // Fare hassasiyeti
    public float gravity = -9.81f;         // Yerçekimi kuvveti
    public float jumpHeight = 1.5f;

    private float xRotation = 0f;          // Kameranýn dikey dönüþ açýsý
    private Vector3 velocity;              // Yerçekimi için düþüþ hýzý


    // Að trafiðini yormamak için son yolladýðýmýz açýyý tutacaðýmýz deðiþken
    private float lastSentPitch = 0f;
    // ReadPermission.Everyone -> Herkes okuyabilir (görebilir).
    // WritePermission.Server -> Sadece server deðiþtirebilir. Bug'dan kurtulmak için owner'dan server a verdik.
    public NetworkVariable<float> networkPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Start()
    {
        // Eðer bu karakter benimse, fare imlecini ekranýn ortasýna kilitle ve gizle
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    private void Update()
    {
        //Eðer obje henüz aðda tam olarak doðmadýysa, hiçbir kod çalýþtýrma!
        if (!IsSpawned) return;

        if (IsOwner)
        {
            HandleMovement();
            HandleMouseLook();
        }
        else
        {
            //Eðer karakter baþkasýnýnsa, 
            // sadece onun að üzerinden gönderdiði kafa açýsýný alýp kameraya uygula.
            cameraTransform.localRotation = Quaternion.Euler(networkPitch.Value, 0f, 0f);
        }

    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Yukarý/Aþaðý bakma hesabý
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -85f, 85f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Saða/Sola bakma (Gövdeyi döndürüyor, ClientNetworkTransform bunu hallediyor zaten)
        transform.Rotate(Vector3.up * mouseX);

        // Eðer açý 1 dereceden fazla deðiþtiyse aða yolla (Gereksiz trafik yaratma)
        if (Mathf.Abs(lastSentPitch - xRotation) > 1f)
        {
            if (IsServer)
            {
                // Host isek zaten Server biziz, direkt deðiþkene yazabiliriz.
                networkPitch.Value = xRotation;
            }
            else
            {
                // Sadece Client isek, Server'dan b izim yerimize yazmasýný rica ediyoruz.
                UpdatePitchServerRpc(xRotation);
            }
            lastSentPitch = xRotation;
        }
    }

    // Client'ýn Server'a gönderdiði talep köprüsü
    [ServerRpc]
    private void UpdatePitchServerRpc(float newPitch)
    {
        networkPitch.Value = newPitch;
    }

    private void HandleMovement()
    {
        // WASD tuþlarýndan gelen veriler
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        if (Input.GetButtonDown("Jump") && controller.isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        } 

        // Karakterin baktýðý yöne göre vektör oluþtur
        Vector3 move = transform.right * x + transform.forward * z;

        // Yürüme iþlemi
        controller.Move(move * speed * Time.deltaTime);

        // Manuel Yerçekimi Uygulamasý
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Yerdeyken dibe yapýþýk kalmasýný saðlar
        }

        velocity.y += gravity * Time.deltaTime; // Düþüþ hýzýný artýr
        controller.Move(velocity * Time.deltaTime); // Düþüþü uygula
    }
}

