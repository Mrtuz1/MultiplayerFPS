using UnityEngine;
using System.Collections;

[RequireComponent(typeof(ParticleSystem))]
public class AutoDestruct : MonoBehaviour
{
    [Header("Ayarlar")]
    [Tooltip("Ýþaretlersen objeyi silmez, sadece kapatýr (Object Pool için ideal).")]
    public bool onlyDeactivate;

    // Bileþeni hafýzada tutacaðýmýz deðiþken
    private ParticleSystem ps;

    private void Awake()
    {
        // Component'i oyun baþlarken SADECE BÝR KERE alýp önbelleðe (cache) atýyoruz.
        ps = GetComponent<ParticleSystem>();
    }

    private void OnEnable()
    {
        // Obje her aktif olduðunda döngüyü baþlat (String kullanmadan, güvenli yol)
        StartCoroutine(CheckIfAlive());
    }

    private IEnumerator CheckIfAlive()
    {
        while (true)
        {
            // Yarým saniye bekle
            yield return new WaitForSeconds(0.5f);

            // Efekt bitti mi diye kontrol et (Önbellekteki 'ps' üzerinden)
            if (!ps.IsAlive(true))
            {
                if (onlyDeactivate)
                {
                    // Havuz sistemi kullanýyorsan objeyi kapat
                    gameObject.SetActive(false);
                }
                else
                {
                    // Havuz sistemi yoksa objeyi komple yok et
                    Destroy(gameObject);
                }

                // Ýþimiz bitti, döngüyü kýr
                break;
            }
        }
    }
}