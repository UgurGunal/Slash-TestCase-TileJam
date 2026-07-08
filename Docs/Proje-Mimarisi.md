# Proje Mimarisi

Bu doküman projenin genel mimarisini ve sistemlerin nasıl çalıştığını basit bir dille anlatır. Katman yapısı, oyun akışı, tile toplama (collect) sistemi ve Order/Rack HUD gibi ana sistemleri kapsar.

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

Rezervasyon/transaction katmanı sayesinde birden fazla tile aynı anda uçabilir. Projeksiyon durumu = commit edilmiş durum + uçuştaki (in-flight) rezervasyonlar.

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
| `CollectReservationService` | Rezervasyon/projeksiyon mantığı: `TryPeekDestination` / `TryReserveCollect` / `TryReserveRackDrain` / `Release` | Gameplay |
| `LevelObjectiveSession` | İnce facade; transaction API'sini `CollectReservationService`'e delege eder | Gameplay |
| `BoardTileCollectCoordinator` | Eşzamanlı uçuş yöneticisi: aktif uçuşlar + FIFO input buffer + `PumpBuffer` | Presentation |
| `RackDrainService` | Rack→order otomatik eşleşme adımları | Gameplay |
| `TileCollectFly` | DOTween uçuş animasyonu | Presentation |

**Bilinen sınır (kabul edilen):** Projeksiyon, order ilerlemesini (bir order tamamlanınca yeni müşteri gelmesi) simüle etmez. Bu yüzden order'ı tamamlayan bir tile uçarken inen başka bir tıklama, yeni açılan order yerine rack'e gidebilir. Güvenli (asla taşmaz), nadir ve bu kapsam için kabul edilebilir.

---

## Rack ve order mantığı (Gameplay)

Saf C# çekirdek. UI'dan tamamen bağımsız.

| Sınıf | Rol |
|-------|-----|
| `LevelObjectiveSession` | İnce facade; alt sistemleri kurar ve dışarıya sadeleştirilmiş bir API sunar (delegasyon) |
| `ActiveOrderSlots` | Ekrandaki aktif müşteriler + kuyruktaki siparişler; ikon doldurma, slot ilerletme ve order eşleşme taraması |
| `RackState` | Rack kapasitesi ve tutulan tile'lar (`TryAdd`, `RemoveAt`) |
| `RackDrainService` | Rack'teki tile'ları uygun order'lara otomatik akıtan adımlar |
| `CollectReservationService` | Uçuştaki (in-flight) rezervasyonlar + projeksiyonlu hedef hesabı (order ikonu / rack slotu) |
| `CollectPipeline` + `MatchOrRackCollectHandler` | Tıklanan tile'ın önce order'a mı yoksa rack'e mi gideceğini belirleyen kural zinciri |

Her sınıfın tek bir değişim ekseni var: order sırası `ActiveOrderSlots`, rack depolama `RackState`, otomatik drenaj `RackDrainService`, rezervasyon/projeksiyon `CollectReservationService`, tekil tile kuralı `CollectPipeline`. `LevelObjectiveSession` bunları birbirine bağlayan ince bir facade olarak kalır.

---

## Tile davranış sistemi (genişletilebilir tile'lar)

Tile artık sadece bir renk (`TileKind`) enum'u değil. Her hücre bir **model** (`BoardCell`: kind + `BehaviorId` + modifier'lar) taşır ve bir **davranış** (`ITileBehavior`) ile eşleşir. Amaç: yeni bir tile tipi (kilitli, buzlu, bomba…) eklerken merkezi dosyalara değiştirmek zorunda kalmamak.

### Tek arayüz, tek kayıt noktası

```
ITileBehavior                         (davranışın kuralları)
  ├─ string Id                        → BoardCell.BehaviorId ile eşleşir
  ├─ IsClickable(...)                 → tıklanabilirlik kapısı
  └─ CanRemoveFromBoard(cell)         → toplandığında tahtadan kalkar mı
     + (opsiyonel) ITileCollectContributor → collect mantığını özelleştir

TileBehaviorCatalog                   (enjekte edilen kayıt defteri, static değil)
  └─ Resolve(behaviorId) → ITileBehavior (bilinmiyorsa StandardTileBehavior)
```

- **`StandardTileBehavior`**: her şeye izin veren varsayılan — **şu an shipping olan tek tile tipi**. Yeni davranışlar bundan türeyip sadece önemsedikleri seam'i override eder.

Bilinçli olarak henüz somut bir özel tile (kilit/buz/bomba vb.) eklenmedi; sistem bunları eklemeye **hazır** ama gereksiz mekanik shipping edilmiyor.

### Kural pipeline'ları katalogdan çözer

`ClickabilityPipeline`, `GameplayRulesContext.CanRemoveFromBoard` ve collect zinciri, davranışı **`TileBehaviorCatalog`'tan** çözer. Katalog `GameCompositionRoot`'ta açıkça kurulur (gizli static lookup yok, test edilebilir):

```
GameCompositionRoot.BuildBehaviorCatalog()
  new TileBehaviorCatalog(new ITileBehavior[] { })   // yeni davranışlar buraya
```

**Yeni tile eklemek = 1 sınıf + bu listeye 1 satır.** Collect'i değiştiren bir tile ayrıca `ITileCollectContributor` implement eder; katalog onu otomatik olarak collect zincirine ekler (`GameplayRulesContext` davranışları tarayıp collect yeteneği olanları toplar). Bilinmeyen bir `behaviorId` sessizce `standard`'a düşer.

### Level JSON'da davranış (opsiyonel, geriye dönük uyumlu)

`matrix3D` ile aynı şekle sahip opsiyonel bir `behaviors` matrisi. Alan yoksa (bugünkü tüm level'lar) her tile `standard`. Örnek (`"ice"` yalnızca formatı göstermek için — böyle bir davranış kayıtlı değilse `standard` gibi davranır):

```json
{
  "width": 3, "height": 1, "depth": 1,
  "matrix3D": [[[0, 1, 2]]],
  "behaviors": [[["standard", "ice", "standard"]]],
  "orders": [[0, 1, 2]]
}
```

Veri akışı: `behaviors` → `LevelGridParser` → `LevelBoardSpec` (kind + behaviorId taşır) → `PlayableBoardState` (`BoardCell`) → `LevelBoardGrid` → `BoardTileView`. Behavior artık zincirin hiçbir yerinde düşmüyor.

### Görsel

`BoardTileView.ApplyBehaviorVisual(overlaySprite, tint)` ile davranışa özel overlay + renk uygulanır. Görsel metadata `TileBehaviorRegistry` (ScriptableObject: id → overlay sprite + tint) içinde; `LevelBoardGrid` çözer. Mantık (`TileBehaviorCatalog`, kod) ile görsel (`TileBehaviorRegistry`, asset) ayrıdır.

### Yeni davranış nasıl eklenir? (örnek reçete)

| İstediğin | Ne yap |
|-----------|--------|
| Tıklanabilirliği değiştir (kilit, buz…) | `class FooTileBehavior : StandardTileBehavior` → `IsClickable`/`CanRemoveFromBoard` override |
| Collect'i değiştir | Ayrıca `ITileCollectContributor` implement et, `TryHandleCollect`'te kendi `cell.BehaviorId`'ini kontrol et |
| Kaydet | `GameCompositionRoot.BuildBehaviorCatalog()` dizisine `new FooTileBehavior()` ekle |
| Level'da kullan | JSON `behaviors` matrisine `"foo"` yaz |
| Görsel ver | `TileBehaviorRegistry` asset'ine id + overlay/tint ekle |

> **Not (oyun dengesi):** Board tile sayısı = order ikon toplamı olmalı (parser doğrular). Tahtadan tile *kaldıran* ya da toplanmasını engelleyen davranışlar bu değişmezliği bozabilir; böyle mekanikler için kazanma koşulunun da güncellenmesi gerekir. Bunlar yeni tile eklerken tasarım kararıdır, çatı bunları destekler.

---

## Order / Rack HUD sistemi

HUD prefab'lardan runtime'da kurulur. Slot sayıları `HudLayoutConfig` ile ayarlanır; her sorumluluk ayrı sınıftadır.

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

| Sınıf | Rol |
|-------|-----|
| `OrderRackHud` | İnce MonoBehaviour facade; session bağlar, animasyon ayarları |
| `OrderRackHudBuilder` | Prefab'lardan HUD kurar |
| `OrderRackHudBinder` | Event subscribe, refresh koordinasyonu |
| `OrderPresenter` | Bir order satırının sprite + animasyon mantığı |
| `RackPresenter` | Rack slot görsellerini günceller |
| `OrderView` / `OrderSlotView` | Order UI görünümü |
| `RackView` / `RackSlotView` | Rack UI görünümü |
| `HudDestinationLayout` | Tile uçuş hedefi Rect çözümü |
| `OrderRackLayoutDiagnostics` | Editor/runtime diagnostic log |

`OrderRackHud` binder'a delege eder ve `DestinationLayout` property'si ile dışarıya canlı layout verir.

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

## Level Editor (Unity Editor aracı)

Level'ları JSON olarak oluşturmak ve düzenlemek için IMGUI tabanlı bir editör penceresi vardır. Oyun mantığından bağımsızdır; `TileJam.LevelEditor` assembly'si `Core`, `LevelData` ve `Presentation` katmanlarını kullanır.

**Açmak:** Unity menüsünden **Window → Tile Level Editor**.

### Temel akış

Editör iki ana fazda çalışır:

```
1. Order Authoring (sipariş yazımı)
   Palette → order sütunlarına tile ekle → Finalize orders
                    │
                    ▼
2. Placing Tiles (tahta yerleştirme)
   Place / Remove → board + rack → Validate → Export
```

| Faz | Ne yaparsın |
|-----|-------------|
| **Order Authoring** | Palette'den tile ekleyerek müşteri sipariş sütunlarını oluşturursun. **Add order** ile yeni sütun açarsın. **Finalize orders** ile siparişleri kilitlersin. |
| **Placing Tiles** | Finalize sonrası tile'lar sırayla eline (hand) gelir; yeşil hücrelere tıklayarak tahtaya yerleştirirsin. Rack, oyundaki gibi geçici depo olarak kullanılır. |

Mevcut bir level yüklendiğinde tahtadaki tile'lar zaten yerleşmiş olabilir. Bu durumda order sütunları boş görünür; tile'lar tahtada kalır.

### Araçlar ve butonlar

| Kontrol | Açıklama |
|---------|----------|
| **Width / Height / Depth** | Tahta boyutu (major cell). **Apply size** ile grid yenilenir. |
| **x1 / x3** | Palette'den tek seferde kaç tile ekleneceği. |
| **Load level** | `Assets/Resources/Levels` altındaki JSON'ları yükler. |
| **Import… / Export…** | Diskten JSON alır / kaydeder. |
| **New** | Boş level başlatır. |
| **Add order** | Yeni müşteri sipariş sütunu ekler (finalize öncesi). |
| **Clear active column** | Aktif sütundaki tile'ları temizler. |
| **Finalize orders** | Siparişleri snapshot'a kilitler; yerleştirme moduna geçer. |
| **Edit orders** | Mevcut level'a yeni sipariş eklemek veya siparişleri değiştirmek için kilidi açar. |
| **Place** | Tile'ı tahtaya veya rack'e koyar. |
| **Remove** | Üstteki tile'ı kaldırır; eşleşen tile order'a, diğerleri rack'e döner. |
| **Remove all** | Tüm tahta + rack tile'larını ilgili order sütunlarına geri toplar. |
| **Validate** | `LevelGridParser` kurallarına göre level'ı kontrol eder. |

### Edit orders modu

Finalize edilmiş bir level'da sipariş değiştirmek için **Edit orders** kullanılır:

1. **Edit orders** → palette ve order sütunları tekrar açılır.
2. **Add order** ile yeni sütun ekle, palette'den tile doldur.
3. İstersen yeni tile'ları hemen tahtaya yerleştir.
4. **Finalize orders** ile yeni siparişleri snapshot'a ekle.

Edit orders sırasında **Place**, **Remove** ve **Remove all** devre dışıdır; önce finalize etmen gerekir.

### Tahta yerleştirme kuralları

- Her tile **2×2 fine cell** kaplar ve grid kesişimine hizalanır.
- Tile'lar katmanlıdır (`depth`); üst katman alttakini örter.
- **Place** modunda: elindeki tile'ı yeşil (boş) alana tıklayarak koyarsın.
- **Remove** modunda: tile'ın herhangi bir yerine tıklamak yeterlidir (2×2 footprint'in tamamı tıklanabilir).
- Rack doluysa veya elinde tile varken yeni tile alamazsın.

### Tile provenance (köken takibi)

Her tahtadaki tile, hangi order sütunundan ve hangi ikon slotundan geldiğini hatırlar (`orderCol` + `orderIcon`). Bu sayede:

- **Remove** ile kaldırılan tile doğru order slot'una geri döner.
- **Remove all** snapshot + henüz finalize edilmemiş yeni order'ları birleştirerek order sütunlarını yeniden doldurur.
- Yeni order eklerken tahta provenance'ı bozulmaz.

### Export kuralları

Export, tahtadaki tile multiset'ini **finalize edilmiş order snapshot** ile karşılaştırır. Şunlar sağlanmalıdır:

| Koşul | Neden |
|-------|-------|
| Elde tile olmamalı | Hand boşaltılmalı. |
| Rack boş olmalı | Tüm tile'lar tahtada olmalı. |
| Yeni order'lar finalize edilmiş olmalı | Export snapshot kullanır; edit modundaki bekleyen siparişler dahil edilmez. |
| Board tile sayısı = order tile sayısı | Oyun kuralı: her order ikonu tahtada bir tile'a karşılık gelir. |

Yeni order tile'larını tahtaya yerleştirip finalize etmeden export edersen editör uyarı verir. **Edit orders → Finalize orders → Export** sırasını izle.

### Mimari (editör sınıfları)

```
TileLevelEditorWindow          → IMGUI pencere, faz/mode yönetimi, import/export
    ├─ LevelEditorOrderModel   → order sütunları, finalize snapshot, reverse-build kuyruğu
    ├─ LevelEditorBoardModel   → tahta grid, rack, provenance, DTO üretimi
    └─ EditorTileIcons         → palette/tile çizimi (sprite veya renk)
```

| Sınıf | Rol |
|-------|-----|
| `TileLevelEditorWindow` | Tüm UI akışı: toolbar, palette, orders, board, validate/export |
| `LevelEditorOrderModel` | Order sütunları, finalize snapshot (`_snapshotAtFinalize`), reverse-build |
| `LevelEditorBoardModel` | 3D grid, rack, tile provenance, board ↔ JSON DTO |
| `EditorTileIcons` | Editörde tile görselleri |

**Snapshot vs live:** Finalize sonrası sipariş tanımı `_snapshotAtFinalize` içinde saklanır. Export bu snapshot'ı yazar. Live order sütunları yerleştirme sırasında tüketilir; tahtadaki tile'lar provenance ile snapshot'a bağlı kalır.

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
| Rezervasyon / projeksiyon mantığı | `CollectReservationService` |
| Tile'ın order/rack kuralı | `CollectPipeline` + `MatchOrRackCollectHandler` |
| Yeni level oluştur / düzenle | **Window → Tile Level Editor** |
| Level JSON export/import | `TileLevelEditorWindow` → Export… / Import… |
| Order finalize / edit akışı | `LevelEditorOrderModel` + **Finalize orders** / **Edit orders** butonları |
