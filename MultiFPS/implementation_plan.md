# 🎮 Multiplayer FPS — Güncel Implementation Planı

> **Proje:** Unity Netcode for GameObjects + Unity Gaming Services  
> **Tarih:** Nisan 2026  
> **Hedef:** Lobby → Relay → Oyun sahnesi akışının tam entegrasyonu

---

## 📋 İçindekiler

1. [Sahne Yapısı](#1-sahne-yapisi)
2. [Lobby → Oyun Akışı (Nasıl Çalışır)](#2-lobby--oyun-akisi)
3. [Her Scriptin Ne Yaptığı](#3-her-scriptin-ne-yaptigi)
4. [UI Hiyerarşisi & Panel Planı](#4-ui-hiyerarsisi--panel-plani)
5. [Kurulum Adımları (Sıralı)](#5-kurulum-adimlari)
6. [Eksik & Yapılacaklar](#6-eksik--yapilacaklar)

---

## 1. Sahne Yapısı

Oyunda **2 (veya 3) sahne** olmalıdır. Düzgün bir NetworkManager yönetimi için sahnelerin rolleri iyi belirlenmelidir.

```text
Assets/Scenes/
├── BootScene.unity       ← Sadece bağlantı ve servis başlatma (İsteğe bağlı, LobbyScene ile birleşebilir)
├── LobbyScene.unity      ← Lobi menüsü, Main Menu, UGS başlatma
└── GameScene.unity       ← Asıl oyun haritası (FPS gameplay)
```

### Açıklama

| Sahne | Neden var? | İçinde ne olur? |
|-------|-----------|-----------------|
| **LobbyScene** | Oyuncuların oyunu açtığı, UGS servislerinin başladığı, oda oluşturup/katıldıkları yer. | LobbyUI panelleri, LobbyManager, RelayManager, NetworkManager. |
| **GameScene** | Gerçek FPS oynanışı. Odadaki herkes aynı anda buraya aktarılır. | Harita, oyuncu spawn noktaları, NetworkObject'ler, silahlar. |

> [!IMPORTANT]  
> **LobbyScene → GameScene** geçişi **NetworkManager.SceneManager** üzerinden yapılır (normal `SceneManager.LoadScene` değil). Bu sayede host sahne değiştirdiğinde tüm bağlı istemciler (clientlar) aynı anda GameScene'e geçer. Sahnelerin Build Settings'e eklenmiş olması zorunludur.

---

## 2. Lobby → Oyun Akışı

### Adım Adım Akış

```text
Oyuncu A (Host)                          Oyuncu B (Client)
─────────────────                        ──────────────────
1. LobbyScene açılır                     1. LobbyScene açılır
2. UGS başlar (anon giriş)               2. UGS başlar (anon giriş)
3. "Oda Oluştur" → LobbyManager.CreateLobbyAsync()
   - UGS'de Lobi oluşturulur
   - Heartbeat başlar (15s aralıkla)
   - Poll başlar (1.5s aralıkla)
                                         3. "Lobiye Katıl" seçer
                                            → LobbyManager.JoinPublicLobbyAsync()
                                            - Odaya girer ve Poll başlar
4. UI'da her iki oyuncu görünür          4. UI'da her iki oyuncu görünür
5. Host "Oyunu Başlat" tıklar
   → LobbyManager.StartGameAsync()
      a) RelayManager.CreateRelayAndStartHost()
         - Relay sunucusunda slot açılır
         - JoinCode alınır (örn: "ABC123")
         - NetworkManager.StartHost() çalışır
      b) Lobi verisi güncellenir:
         RelayCode = "ABC123"
         GameStarted = "true"
                                         5. Client'in Poll döngüsü "GameStarted=true" ve Relay Code'u görür
                                            → RelayManager.JoinRelayAndStartClient("ABC123")
                                            - Kod ile Relay'e bağlanır
                                            - NetworkManager.StartClient() çalışır
6. Host, NetworkManager ile GameScene'i yükler
   ← TÜM BAĞLI İSTEMCİLER AYNI ANDA OYUN SAHNESİNE GEÇİŞ YAPAR →
```

> [!NOTE]  
> `LobbyManager` ve `RelayManager` scriptleri `Awake` metodunda `DontDestroyOnLoad` ile işaretlenmiştir. Sahne değişse bile yok olmazlar.

---

## 3. Her Scriptin Ne Yaptığı

Projede bulunan mevcut C# dosylarının görevleri:

### 🔧 Lobby ve Bağlantı Yönetimi

- **`LobbyManager.cs`**: 
  - UGS (Unity Gaming Services) Lobby API'siyle tüm iletişimi yönetir.
  - Servisi başlatır (`InitializeAsync`), Lobi oluşturur (`CreateLobbyAsync`), Lobileri listeler (`ListPublicLobbiesAsync`), Odaya kod ile (`JoinLobbyByCodeAsync`) veya listeden (`JoinPublicLobbyAsync`) katılır.
  - Odanın zaman aşımına uğramaması için periyodik Heartbeat atar. Host oyunu başlattığında Lobiye Relay kodunu kaydeder. Client'lar Poll döngüsü ile lobiyi dinler ve Relay kodunu alınca oyuna bağlanır.
- **`RelayManager.cs`**: 
  - Unity Relay servisini kullanarak oyuncuların birbirine bağlanmasını sağlar. Port yönlendirme derdini ortadan kaldırır. 
  - `CreateRelayAndStartHost` ile Host için bir tahsis (allocation) yapar ve join codu alır.
  - `JoinRelayAndStartClient` ile Client'ın hostun paylaştığı kod ile o odaya Relay üzerinden bağlanmasını sağlar.
- **`LobbyUI.cs`**: 
  - Tüm lobi panellerini yönetir. UI butonları ile etkileşim sağlar. Loading ekranı, Hata mesajları, oda içi oyuncu listesi gibi arayüz güncellemelerini `LobbyManager`'dan gelen olayları dinleyerek gerçekleştirir.
- **`LobbyListItem.cs`**: 
  - `LobbyUI`'daki "Lobi Listesi" ekranındaki tek bir odanın (satırın) UI kontrolcüsüdür. Oda adını, oyuncu sayısını ve varsa kilit ikonunu (şifreli odalar için) gösterir.

### 🔧 Oyun İçi (Gameplay) Sistemler

- **`PlayerSetup.cs`**: 
  - Oyuncu Local Player (kendi karakteri) mi yoksa ağdaki diğer oyunculardan biri mi olduğunu ayrıştırır. Kamerayı sadece yerel oyuncu için aktif eder, diğer oyuncuların kameralarını kapatır.
- **`PlayerMovement.cs`**: 
  - Karakterin hareketini ve rotasyonunu sağlayan basit scripttir. Sadece objenin sahibi (`IsOwner`) olan oyuncu tarafından kontrol edilmesini sağlar. Transform eşitlemeleri genellikle NetworkTransform (veya ClientNetworkTransform) ile yapılarak diğer istemcilere senkronize edilir.
- **`PlayerCombat.cs`**: 
  - Ateş etme, silah kullanma mantığı. Hasar olaylarını Server RPC (Server'da çalışan fonksiyonlar) vasıtasıyla ileterek tüm istemcilere (veya vurulan kişiye) bildirir.
- **`PlayerHealthManager.cs`**: 
  - Oyuncuların can sistemini yönetir. Sağlık verisini senkronize tutmak için muhtemelen `NetworkVariable<int/float>` kullanır, ölüm durumlarını denetler.
- **`AutoDestruct.cs`**: 
  - Mermi, efekt (kan/kıvılcım vb.) gibi objelerin sahnede sürekli birikerek performans sorunu yaratmaması için belirli bir süre sonra otomatik yok edilmelerini sağlar.

---

## 4. UI Hiyerarşisi & Panel Planı

### LobbyScene Canvas Hiyerarşisi

```text
Canvas
 ├── LobbyUI (Script buraya eklenir)
 │
 ├── MainMenuPanel (İlk açılan ekran)
 │    ├── Oyuncu Adı Input (Opsiyonel)
 │    ├── Oda Oluştur Butonu
 │    ├── Lobi Listesi Butonu
 │    └── Kod ile Katıl Butonu
 │
 ├── CreateLobbyPanel
 │    ├── Oda Adı Input
 │    ├── Max Oyuncu Dropdown (2, 4, 6, 8 vs.)
 │    ├── Gizli Oda Toggle
 │    ├── Şifre Input (Eğer Gizli açıksa görünür)
 │    ├── Oluştur Butonu (LobbyManager.CreateLobbyAsync)
 │    └── Geri Butonu
 │
 ├── LobbyListPanel
 │    ├── ScrollView -> Content (Buraya prefablar oluşturulacak)
 │    ├── Yenile Butonu
 │    └── Geri Butonu
 │
 ├── JoinByCodePanel
 │    ├── Lobi Kodu Input
 │    ├── Şifre Input
 │    ├── Katıl Butonu
 │    └── Geri Butonu
 │
 ├── PasswordModalPanel (Şifreli lobiye tıklanınca sorulan şifre popup'ı)
 │
 ├── LobbyRoomPanel (Odaya girildiğinde açılan, oyuncuları beklediğiniz oda ekranı)
 │    ├── Oda İsmi Text
 │    ├── Oda Kodu Text (Host olan kişide lobi kodunu başkasına vermek için)
 │    ├── Oyuncu Listesi (Vertical Layout Group, her oyuncu için bir metin)
 │    ├── Oyunu Başlat Butonu (SADECE HOST görür, GameScene yükler)
 │    └── Odadan Çık Butonu (Lobiye döner)
 │
 ├── LoadingPanel (İşlem devam ederken arkaplan panelini kaplar, "Yükleniyor..")
 └── ErrorText (Bağlantı/şifre hatalarını kırmızı ile gösteren text)
```

> **Prefab İhtiyaçları:**
> - `LobbyListItem` Prefab: İçinde oda ismi texti, oyuncu sayısı texti, katılma butonu olan tek satırlık UI elementi.
> - `PlayerListItem` Prefab: Lobi odası ekranında oyuncu isimlerini gösterecek text elementi.

---

## 5. Kurulum & Aktarma Adımları (Sıralı)

Lobi sistemini projeye aktarmak için şu adımları izleyin:

1. **Unity Gaming Services Ayarları:**
   - Unity'de Edit -> Project Settings -> Services bölümünden projenizi UGS sistemine bağlayın.
   - Lobby ve Relay servislerinin Unity Dashboard (Website) üzerinden aktif edildiğine ve Authentication yapıldığına emin olun.

2. **NetworkManager Ayarları:**
   - Hierarchy'de yeni bir `NetworkManager` oluşturun veya olanı inceleyin.
   - Component olarak `NetworkManager` ve `UnityTransport` bağlı olmalıdır.
   - **`Enable Scene Management`** seçeneği `NetworkManager` üzerinden KESİNLİKLE işaretli olmalıdır.

3. **LobbyScene Hazırlığı:**
   - Yeni bir sahne oluşturup adını `LobbyScene` yapın.
   - Yukarıdaki **UI Hiyerarşisi**nde belirtildiği gibi Canvas ve Panelleri tasarlayın. Panellerin hepsini iç içe (`LobbyUI` scriptinin inspector alanlarına) referanslayın.
   - Boş Gameobject'ler oluşturup üzerlerine `LobbyManager`, `RelayManager` ekleyin. 

4. **GameScene Hazırlığı:**
   - `GameScene` tamamen boş oyun haritası, aydınlatma ve spawn pointlerinden oluşmalıdır. 
   - `NetworkManager`'da Player Prefab olarak, içine `PlayerSetup`, `PlayerMovement`, `PlayerCombat`, `PlayerHealthManager` ve `NetworkObject` bileşenleri atanmış oyuncu objenizi seçin.

5. **Build Ayarları (KRİTİK):**
   - Sahnelerin sırasını File -> Build Settings altından şöyle belirleyin:
     `0 - LobbyScene`
     `1 - GameScene` 
   - Eğer `LobbyManager` içindeki `GAME_SCENE_NAME` değişkeninde "GameScene" yazıyorsa (ki yazıyor), sahnelerdeki isim bununla birebir aynı olmalıdır.

---

## 6. Eksik & Yapılacaklar Listesi

Projenin tam çalışması için hala eksik olabilecek kısımlar:

- [x] LobbyUI panellerinin tasarımının Unity üzerinde oluşturulması.
- [x] LobbyListItem (Lobi Listesi satırı) ve PlayerListItem (Oda içi oyuncu satırı) Prefab'larının oluşturulması ve LobbyUI inspector alanlarına atılması.
- [x] NetworkPlayer Prefab'ına gerekli Network ve Player scriptlerinin ekli olduğunun teyit edilmesi.
- [x] Birden fazla kişiyle test için ParrelSync paketinin kurulması veya oyunun Client Build'ının alınıp test edilmesi. (Tek makinede tek IP ile Relay testleri bazen çakışabilir, ParrelSync önerilir).
