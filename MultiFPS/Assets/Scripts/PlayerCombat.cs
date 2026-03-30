using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerCombat : NetworkBehaviour
{
    [Header("Referanslar")]
    public Transform gunBarrel;
    public Camera playerCamera;
    public GameObject muzzleEffect;
    public GameObject impactEffect;
    public AudioClip gunAudio;
    private AudioSource playerAudioSource;

    [Header("Ayarlar")]
    public int damage = 25;
    public float range = 100f;

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
            // Týkladýðýmýz an kendi ekranýmýzda anýnda efektleri oynatýyoruz ki lag hissi olmasýn.
            PlayShootEffects();

            // Server'a "Ben ateþ ettim, vurup vurmadýðýmý hesapla ve diðerlerine haber ver" diyoruz.
            ShootServerRpc();
        }
    }

    // Ortak efekt kodunu tek bir yere topladýk ki tekrar tekrar ayný þeyi yazmayalým
    private void PlayShootEffects()
    {
        Instantiate(muzzleEffect, gunBarrel.position, gunBarrel.rotation, gunBarrel);
        playerAudioSource.PlayOneShot(gunAudio);
    }

    [ServerRpc]
    private void ShootServerRpc()
    {
        // Raycast'i (ýþýn gönderme iþlemini) Sunucuda (Server) yapýyoruz ki oyuncular hile yapamasýn (Hit Validation).
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward, out RaycastHit hit, range))
        {
            if (hit.transform.TryGetComponent(out PlayerHealthManager playerWhoDamaged))
            {
                // Bir oyuncuyu vurduk!
                playerWhoDamaged.TakeDamage(damage);

                // Oyuncuyu vurduðumuzda duvar efekti çýkmasýn, ama diðerleri silah sesimizi duysun.
                ShootClientRpc(false, Vector3.zero, Vector3.zero);
            }
            else
            {
                // Duvar, zemin gibi baþka bir objeye vurduk.
                ShootClientRpc(true, hit.point, hit.normal);
            }
        }
        else
        {
            // Havaya sýktýk (Raycast hiçbir þeye çarpmadý). Yine de mermi sesi/ýþýðý diðerlerine gitmeli.
            ShootClientRpc(false, Vector3.zero, Vector3.zero);
        }
    }

    [ClientRpc]
    private void ShootClientRpc(bool hitWall, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Eðer bu kodu çalýþtýran kiþi silahý SIKMAYAN biriyse (diðer oyunculardan biriyse) 
        // alev ve ses efektini oynat. Sýkan kiþi zaten HandleShot'ta oynattý.
        if (!IsOwner)
        {
            PlayShootEffects();
        }

        // Mermi izini HERKES görecek (silahý sýkan dahil). O yüzden bu kýsmý if'in dýþýna aldýk.
        if (hitWall)
        {
            Instantiate(impactEffect, hitPoint, Quaternion.LookRotation(hitNormal));
        }
    }
}