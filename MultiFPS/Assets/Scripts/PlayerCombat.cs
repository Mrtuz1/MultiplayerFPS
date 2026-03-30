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
        // RaycastAll: Iþýnýn çarptýðý BÜTÜN objeleri bir dizi (array) olarak alýr.
        RaycastHit[] hits = Physics.RaycastAll(playerCamera.transform.position, playerCamera.transform.forward, range);

        RaycastHit closestValidHit = new RaycastHit();
        float closestDistance = Mathf.Infinity;
        bool foundValidHit = false;

        // Çarptýðýmýz bütün objeleri tek tek kontrol ediyoruz
        foreach (RaycastHit hit in hits)
        {
            // Eðer çarptýðýmýz obje BÝZÝM karakterimizse (veya karakterin altýndaki bir parçaysa), bunu yok say ve sýradakine geç (continue).
            // transform.root objenin en tepesindeki ana objeyi verir.
            if (hit.transform.root == this.transform.root) continue;

            // Eðer çarptýðýmýz þey biz deðilsek ve kameraya daha yakýnsa, bunu geçerli vuruþ olarak kaydet.
            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestValidHit = hit;
                foundValidHit = true;
            }
        }

        // Eðer biz hariç geçerli bir þeye çarptýysak
        if (foundValidHit)
        {
            // Adam mý vurduk?
            if (closestValidHit.transform.TryGetComponent(out PlayerHealthManager playerWhoDamaged))
            {
                playerWhoDamaged.TakeDamage(damage);
                ShootClientRpc(false, Vector3.zero, Vector3.zero);
            }
            else // Duvar falan mý vurduk?
            {
                ShootClientRpc(true, closestValidHit.point, closestValidHit.normal);
            }
        }
        else // Hiçbir þeye çarpmadýk (Havaya sýktýk)
        {
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