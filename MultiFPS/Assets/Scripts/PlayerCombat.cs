using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerCombat : NetworkBehaviour
{
    [Header("Referanslar")]
    public Transform gunBarrel; // Namlu ucu (ileride efektleri buradan patlatacaðýz)
    public Camera playerCamera; // FPS kameramýz (ýþýn buradan çýkacak)

    [Header("Ayarlar")]
    public int damage = 25; // Hasar miktarý
    public float range = 100f; // Silahýn menzili


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
        // Mermi sesi çalma, namlu alevi (muzzle flash) patlatma kodlarý buraya gelecek.
        Debug.Log("[Client]: Pew pew! (Efektler çalýþtý)");
    }
}
