# Proje Mimarisi

Bu doküman projenin genel mimarisini ve sistemlerin nasıl çalıştığını basit bir dille anlatır. Sadece HUD'a değil; katman yapısı, oyun akışı, tile toplama (collect) sistemi ve Order/Rack HUD gibi tüm ana sistemlere değinir.

Her başlıkta önce (varsa) eski yaklaşımın kısa özeti, ardından şu anki yapının nasıl işlediği anlatılır.

---

## Genel bakış

TileJam, tahtadaki (board) tile'lara tıklayıp bunları müşteri siparişlerine (order) veya geçici bir depoya (rack) toplama üzerine kurulu bir puzzle oyunu.

Ana sistemler:

- **Board / Level yükleme** — JSON'dan level okunur, tahta runtime'da kurulur.
- **Collect (tile toplama)** — tıklanan tile bir order ikonuna veya rack slotuna gider; rack dolarsa level başarısız olur.
- **Order / Rack HUD** — aktif siparişler ve rack durumu ekranda gösterilir.
- **Level Editor** — tahtayı editörde düzenleme araçları.

---

## Katman mimarisi (assembly'ler)

Proje beş assembly'ye (asmdef) bölünmüş. Bağımlılıklar tek yönlü akar; alttaki katman üsttekini bilmez.

```
┌──────────────────────────────────────────────────────────┐
│  TileJam.LevelEditor   (Editor araçları)                   │
│      → Core, LevelData, Presentation                       │
└──────────────────────────────────────────────────────────┘
                    │
                    ▼
┌──────────────────────────────────────────────────────────┐
│  TileJam.Presentation  (Unity/MonoBehaviour, UI, animasyon)│
│      → Core, LevelData, Gameplay                           │
└──────────────────────────────────────────────────────────┘
                    │
                    ▼
┌──────────────────────────────────────────────────────────┐
│  TileJam.Gameplay      (SAF C# oyun mantığı, engine yok)   │
│      → Core, LevelData                                     │
└──────────────────────────────────────────────────────────┘
                    │
                    ▼
┌──────────────────────────────────────────────────────────┐
│  TileJam.LevelData     (level şeması, JSON parse)          │
│      → Core                                                │
└──────────────────────────────────────────────────────────┘
                    │
                    ▼
┌──────────────────────────────────────────────────────────┐
│  TileJam.Core          (sabitler, TileKind, event bus)     │
│      → (bağımlılık yok)                                    │
└──────────────────────────────────────────────────────────┘
```

**Önemli nokta:** `TileJam.Gameplay` ve `TileJam.LevelData`, `noEngineReferences: true` ile işaretli — yani hiçbir UnityEngine tipine bağlı değiller. Tüm order/rack/collect kuralları saf C#. Bu sayede oyun mantığı Unity'den bağımsız düşünülebilir ve (istenirse) düz unit test ile denenebilir. UI ve animasyon gibi Unity'ye bağlı her şey `Presentation` katmanında durur.

---

## Oyun akışı (uçtan uca)

1. **Awake (-200):** `OrderRackHudBuilder` → `HudLayoutConfig`'i okur, order/rack prefab'larını spawn eder, `OrderRackHud`'a view'ları verir.
2. **Awake (-100):** `GameCompositionRoot` → `LevelBoardLoader`'ı initialize eder.
3. **Reload:** `LevelBoardLoader` → level JSON yükler, `LevelObjectiveSession` oluşturur (config'teki slot sayılarını kullanır), `OrderRackHud.BindSession` çağırır, tahtayı kurar.
4. **Tile tıklama:** `BoardTileCollectCoordinator` → session'a collect uygular, tile uçuş animasyonu için `OrderRackHud.DestinationLayout` üzerinden hedef Rect bulur.
5. **Event:** Session event yayınlar → `OrderRackHudBinder` → presenter'lar view'ları günceller.

---

## Collect (tile toplama) akışı

Bir tile'a tıklandığında ne olacağını yöneten sistem. Girdi (`BoardTileView` tıklaması) → oyun kuralı (order eşleşmesi / rack) → görsel geri bildirim (uçuş animasyonu) → rack→order otomatik drenajı.

### Eskiden — tek uçuşlu kilit (single-flight)

`BoardTileCollectCoordinator` tek bir `_tileCollectInFlight` boolean'ı ile çalışıyordu:

- Animasyon oynarken (veya order tamamlanınca gelen rack-drain zinciri sürerken) gelen yeni tıklamalar sessizce **düşüyordu** — kuyruklanmıyordu.
- Hedef (order ikonu / rack slotu) tıklama anında `TryPeekCollectDestination` ile **okunuyor**, ama gerçek mutasyon (`TryCollectTile`) ancak tween bitince yapılıyordu. Aradaki boşlukta hiçbir şey rezerve edilmediği için gerçek eşzamanlılık (concurrency) mümkün değildi; aynı anda iki uçuş çakışabilir, rack taşabilir veya aynı order ikonu iki kez dolabilirdi.
- Sonuç: hızlı tıklayan oyuncunun girdileri kayboluyordu, akış seri (sırayla) çalışıyordu.

### Şimdi — rezervasyon + transaction + input buffer

Domain seviyesinde bir **rezervasyon/transaction** katmanı eklendi. Projeksiyon durumu = commit edilmiş durum + uçuştaki (in-flight) rezervasyonlar.

Akış:

```
Tile tıklandı
   │
   ▼
Tıklanabilir & session aktif mi? ──hayır──► yoksay
   │ evet
   ▼
session.TryReserveCollect(kind)
   │
   ├─ rezerve edildi (order ikonu / projeksiyon rack slotu)
   │      ▼
   │   Tile'ı ayır (detach), eşzamanlı uçuşu başlat
   │      ▼
   │   Tween bitti → CommitReservation → TryCollectTile
   │      ▼
   │   Order tamamlandıysa → animasyonlu rack drenajı
   │      ▼
   │   PumpBuffer (kapasite açıldıysa bekleyen tıklamaları dene)
   │
   └─ rezerve EDİLEMEDİ (projeksiyon rack dolu)
          ▼
       FIFO input buffer'a ekle (tile tahtada kalır)
          ▼
       Bir uçuş indiğinde tekrar denenir; hâlâ doluysa ancak o zaman fail
```

Öne çıkanlar:

- **Gerçek eşzamanlılık:** Birden fazla tile aynı anda uçabilir. Her uçuş kendi hedefini önden rezerve ettiği için rack taşmaz, aynı order ikonu iki kez dolmaz.
- **Girdi kaybı yok:** Rezerve edilemeyen tıklamalar FIFO input buffer'a alınır ve kapasite açılınca (bir uçuş inince / rack drenajı slot boşaltınca) yeniden denenir.
- **Projeksiyonlu kapasite:** "Rack dolu mu?" kararı `commit + in-flight` toplamına göre verilir, sadece commit edilmiş sayıya göre değil.

### İlgili sınıflar

| Sınıf | Rol | Katman |
|-------|-----|--------|
| `CollectReservation` | Tek bir uçuşun rezervasyonu (hedef, rack delta, order ikonu) | Gameplay |
| `CollectReservationBook` | Açık rezervasyonlar + toplam projeksiyon rack delta | Gameplay |
| `LevelObjectiveSession` | `TryReserveCollect` / `TryReserveRackDrain` / `CommitReservation` / `Cancel*` transaction API'si | Gameplay |
| `BoardTileCollectCoordinator` | Eşzamanlı uçuş yöneticisi: aktif uçuşlar + FIFO input buffer + `PumpBuffer` | Presentation |
| `RackDrainService` | Rack→order otomatik eşleşme adımları (değişmedi) | Gameplay |
| `TileCollectFly` | DOTween uçuş animasyonu | Presentation |

**Bilinen sınır (kabul edilen):** Projeksiyon, order ilerlemesini (bir order tamamlanınca yeni müşteri gelmesi) simüle etmez. Bu yüzden order'ı tamamlayan bir tile uçarken inen başka bir tıklama, yeni açılan order yerine rack'e gidebilir. Güvenli (asla taşmaz), nadir ve bu kapsam için kabul edilebilir.

---

## Rack ve order mantığı (Gameplay)

Saf C# çekirdek. UI'dan tamamen bağımsız.

| Sınıf | Rol |
|-------|-----|
| `LevelObjectiveSession` | Order queue, rack ve collect kurallarının facade'ı |
| `ActiveOrderSlots` | Ekrandaki aktif müşteriler + kuyruktaki siparişler; ikon doldurma ve slot ilerletme |
| `RackState` | Rack kapasitesi ve tutulan tile'lar (`TryAdd`, `RemoveAt`) |
| `RackDrainService` | Rack'teki tile'ları uygun order'lara otomatik akıtan adımlar |
| `CollectPipeline` + `MatchOrRackCollectHandler` | Tıklanan tile'ın önce order'a mı yoksa rack'e mi gideceğini belirleyen kural zinciri |

---

## Order / Rack HUD sistemi

### Eskiden — ne vardı, ne sorun çıkarıyordu?

**1. Tek script, çok iş (`OrderRackHud`)**

Eski `OrderRackHud` (ve arada kullanılan `OrderRackHudController`) şunların hepsini aynı yerde topluyordu: session event'lerini dinlemek, order/rack görsellerini güncellemek, tile uçuşu için hedef RectTransform bulmak, order tamamlanınca scale animasyonu, layout diagnostic log'ları. Bu, view katmanında **Single Responsibility** ihlaliydi.

**2. Sahne modüler değildi**

Rack slot'ları ve order satırları sahneye tek tek Image olarak konmuştu. Rack'i 6'dan 9 slota çıkarmak sahne + kod değişikliği gerektiriyordu.

**3. İsimlendirme kafa karıştırıcıydı**

| Eski isim | Ne anlama geliyordu | Sorun |
|-----------|---------------------|-------|
| `OrderStripSlot` | Bir müşteri siparişi satırı | "Strip" ve "Slot" karışıyordu |
| `RackBar` | Oyuncunun tile tuttuğu rack | "Bar" belirsiz |
| `OrderIcon` | Siparişteki tek bir ikon | Aslında slot (ikon + tik) |
| `OrderRackManager` | HUD GameObject'i | Ne yaptığı isimden anlaşılmıyordu |
| `HudBuilder` | Prefab'lardan HUD kuran script | Genel isim, spesifik değil |

### Şimdi — modüler, prefab tabanlı HUD

HUD prefab'lardan runtime'da kuruluyor, slot sayıları `HudLayoutConfig` ile ayarlanıyor ve her sorumluluk ayrı sınıfta.

```
┌─────────────────────────────────────────────────────────────┐
│  SAHNE (Main.unity)                                         │
│  ObjectiveHud GameObject                                    │
│    ├─ OrderRackHudBuilder   → prefab'lardan UI kurar         │
│    └─ OrderRackHud          → ince facade (session bağlar)  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  OrderRackHudBinder         → event dinler, koordine eder   │
│    ├─ OrderPresenter[]      → her order satırının mantığı   │
│    ├─ RackPresenter         → rack slot görselleri          │
│    └─ HudDestinationLayout  → tile uçuş hedefi (Rect)       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  VIEW (prefab'lar)                                          │
│    OrderView      → bir müşteri siparişi satırı             │
│    OrderSlotView  → satırdaki tek ikon + tik                │
│    RackView       → rack çerçevesi + slot container         │
│    RackSlotView   → rack'teki tek slot (arka plan + ikon)   │
└─────────────────────────────────────────────────────────────┘
```

### Prefab hiyerarşisi

Dört atom prefab + iki container prefab:

```
Order.prefab                    Rack.prefab
└─ Container (Horizontal)       └─ SlotContainer (Horizontal)
   └─ (runtime: OrderSlot x N)     └─ (runtime: RackSlot x N)

OrderSlot.prefab                RackSlot.prefab
├─ OrderIcon (Image)            ├─ Background (Image, her zaman görünür)
└─ Tick (Image, başta kapalı)   └─ Icon (Image, tile varken açık)
```

**Önemli ayrım:**

| Referans | Nerede | Ne işe yarar |
|----------|--------|--------------|
| `orderContainer` / `rackContainer` | `OrderRackHudBuilder` (sahne) | Builder'ın prefab spawn edeceği **boş RectTransform** alanları |
| `container` / `slotContainer` | `Order.prefab` / `Rack.prefab` içinde | Spawn edilen slot'ların parent'ı |

Sahne container'ı "nereye koyayım", prefab container'ı "slot'ları nereye dizeyim".

### Sınıf sorumlulukları (HUD)

| Sınıf | Rol | Eskiden neredeydi |
|-------|-----|-------------------|
| `OrderRackHud` | İnce MonoBehaviour facade; session bind, animasyon ayarları | Her şey buradaydı |
| `OrderRackHudBuilder` | Prefab'lardan HUD kurar | Yoktu / `HudBuilder` |
| `OrderRackHudBinder` | Event subscribe, refresh koordinasyonu | `OrderRackHudController` |
| `OrderPresenter` | Bir order satırının sprite + animasyon mantığı | `OrderStripPresenter` |
| `RackPresenter` | Rack slot Image'larını günceller | `RackBarPresenter` |
| `OrderView` / `OrderSlotView` | Order UI görünümü | `OrderStripView` / `OrderIconView` |
| `RackView` / `RackSlotView` | Rack UI görünümü | `RackBarView` / yoktu |
| `HudDestinationLayout` | Tile uçuş hedefi Rect çözümü | `OrderRackHud` içindeydi |
| `OrderRackLayoutDiagnostics` | Editor/runtime diagnostic log | `OrderRackHud` içindeydi |

**OrderRackHud artık ne yapmıyor?** Event dinlemiyor, görselleri güncellemiyor, rect çözmüyor, diagnostic yazmıyor. Sadece binder'a delege ediyor ve `DestinationLayout` property'si ile dışarıya canlı layout veriyor.

---

## HudLayoutConfig — tek ayar noktası

Dosya: `Assets/GameData/HudLayoutConfig.asset`

| Alan | Varsayılan | Etkisi |
|------|------------|--------|
| `rackCapacity` | 6 | Rack'te kaç slot spawn edilir + gameplay rack kapasitesi |
| `activeOrderSlotCount` | 2 | Ekranda kaç order satırı + gameplay aktif slot sayısı |
| `orderPrefab` | Order.prefab | Müşteri satırı şablonu |
| `orderSlotPrefab` | OrderSlot.prefab | Satırdaki ikon+tick şablonu |
| `rackPrefab` | Rack.prefab | Rack çerçevesi şablonu |
| `rackSlotPrefab` | RackSlot.prefab | Rack slot şablonu |

**Kim okur?**

- `OrderRackHudBuilder` → görsel kurulum (kaç prefab spawn edilecek)
- `LevelBoardLoader` → gameplay (`LevelObjectiveSession` constructor'a kapasite geçer)

Böylece UI ve oyun mantığı aynı sayıları kullanır; biri 6 diğeri 9 olmaz.

**Slot tipini değiştirmek:** Prefab'ın kendisini düzenle (`OrderSlot.prefab` veya `RackSlot.prefab`). Config'te referans aynı kalır, tüm instance'lar güncellenir.

---

## İleride neyi kolayca değiştirebilirsin?

| İstediğin değişiklik | Ne yap |
|----------------------|--------|
| Rack 9 slot olsun | `HudLayoutConfig` → `rackCapacity = 9` |
| 3 aktif order satırı | `HudLayoutConfig` → `activeOrderSlotCount = 3` |
| Order slot görünümü | `OrderSlot.prefab` düzenle |
| Rack slot görünümü | `RackSlot.prefab` düzenle |
| Order tamamlanma animasyonu | `OrderRackHud` Inspector → `orderCompleteScale*` alanları |
| Tile uçuş hedefi | `HudDestinationLayout` (genelde dokunmana gerek yok) |
| Eşzamanlı collect davranışı | `BoardTileCollectCoordinator` (rezervasyon + input buffer) |
| Tile'ın order/rack kuralı | `CollectPipeline` + `MatchOrRackCollectHandler` |
