using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerCombat : NetworkBehaviour
{
    [Header("Referanslar")]
    public Transform gunBarrel; // Namlu ucu (ileride efektleri buradan patlatacaðýz)
    public Camera playerCamera; // FPS kameramýz (ýþýn buradan çýkacak)
    public GameObject muzzleEffect;
    public AudioClip gunAudio;
    private AudioSource playerAudioSource;

    [Header("Ayarlar")]
    public int damage = 25; // Hasar miktarý
    public float range = 100f; // Silahýn menzili

    private void Awake()
    {
        playerAudioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleShot();
    }

    private void HandleShot()
    {
        if (Input.GetMouseButtonDown(0))
        {
            ShootServerRpc();
        }
    }
    [ServerRpc]
    private void ShootServerRpc()
    {
        RaycastHit hit;
        if(Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out hit, range)){

            if (hit.transform.TryGetComponent(out PlayerHealthManager playerWhoDamaged))
            {
                playerWhoDamaged.TakeDamage(damage);
            }
        }
        ShootClientRpc();
    }

    [ClientRpc]
    private void ShootClientRpc()
    {
        // BU KISIM BÜTÜN OYUNCULARDA (Sen dahil) ÇALIÞIR
        Instantiate(muzzleEffect, gunBarrel.position, gunBarrel.rotation, gunBarrel);

        playerAudioSource.PlayOneShot(gunAudio);
    }
}
