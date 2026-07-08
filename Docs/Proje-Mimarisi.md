# Proje Mimarisi

Bu doküman, projeye yeni gelen biri için yazıldı. Oyunun ne yaptığını, kodun hangi parçalara ayrıldığını ve bir tile tıklandığında neler olduğunu anlatır.

## Oyunu 30 saniyede anlamak

Oyuncu tahtadaki **tile**'lara tıklar. Tıklanan tile:

1. **Order** (müşteri siparişi) satırındaki uygun bir ikona gidebilir — siparişteki tüm ikonlar dolunca o müşteri tamamlanır, sıradaki müşteri gelir.
2. Uygun order yoksa **rack**'e (geçici depo) gider — rack dolarsa level kaybedilir.

Ekranın üstünde aktif siparişler ve rack görünür (**HUD**). Tahta ve level verisi JSON dosyasından yüklenir.

---

## Katman mimarisi

Kod beş **assembly** (derleme birimi) içinde. Ok tuşu **aşağı = temel**, **yukarı = o temele dayanan üst katman** demek. Level Editor en üstte çünkü oyunu çalıştırmaz; diğer katmanları *kullanan* bir araçtır — temel değildir.

```
                    ┌─────────────────────────────┐
   en üst (araç)    │  TileJam.LevelEditor        │
                    │  Unity editör penceresi     │
                    └──────────────┬──────────────┘
                                   │ kullanır
                    ┌──────────────▼──────────────┐
   oyun sahnesi     │  TileJam.Presentation       │
                    │  UI, animasyon, MonoBehaviour│
                    └──────────────┬──────────────┘
                                   │ kullanır
                    ┌──────────────▼──────────────┐
   oyun kuralları   │  TileJam.Gameplay           │
                    │  saf C# — Unity yok         │
                    └──────────────┬──────────────┘
                                   │ kullanır
                    ┌──────────────▼──────────────┐
   level verisi     │  TileJam.LevelData          │
                    │  JSON şeması, parse         │
                    └──────────────┬──────────────┘
                                   │ kullanır
                    ┌──────────────▼──────────────┐
   temel            │  TileJam.Core               │
                    │  TileKind, sabitler, event  │
                    └─────────────────────────────┘
```

### Her katman ne yapar?

| Katman | Düz Türkçe | Örnek sınıflar / dosyalar |
|--------|------------|---------------------------|
| **Core** | Herkesin ortak kullandığı temel tipler ve sabitler | `TileKind`, `GameConstants`, event bus |
| **LevelData** | Level JSON'unun okunması ve doğrulanması; Unity'den bağımsız | `LevelGridParser`, `OrderSpec`, `BoardCell` |
| **Gameplay** | Oyun kuralları: order doluyor mu, rack dolu mu, collect nereye gider | `LevelObjectiveSession`, `ActiveOrderSlots`, `RackState` |
| **Presentation** | Oyuncunun gördüğü her şey: tahta, HUD, uçuş animasyonu | `LevelBoardLoader`, `OrderRackHud`, `BoardTileView` |
| **LevelEditor** | Level yazma aracı; runtime oyunu değil, editör penceresi | `TileLevelEditorWindow` |

**Neden Gameplay Unity'den ayrı?** `Gameplay` ve `LevelData` içinde `UnityEngine` kullanılmaz (`noEngineReferences: true`). Böylece order/rack kuralları saf C# kalır; istenirse Unity açmadan test edilebilir. Buton, Image, DOTween gibi şeyler yalnızca `Presentation`'dadır.

**Presentation ↔ Gameplay sınırı:** Gameplay state değiştirir ve event yayınlar. Presentation event'i dinler, ekranı günceller. HUD, `IObjectiveHudState` ile yalnızca *okuma* yapar; tile toplama kuralını bilmez.

---

## Uçtan uca akış (oyun açılınca)

1. **Awake (-200):** `OrderRackHudBuilder` — `HudLayoutConfig`'ten kaç order satırı / rack slotu olacağını okur, prefab'ları spawn eder.
2. **Awake (-100):** `GameCompositionRoot` — `LevelBoardLoader`'ı hazırlar.
3. **Reload:** `LevelBoardLoader` — `Assets/Resources/Levels/*.json` yükler, `LevelObjectiveSession` oluşturur, HUD'a bağlar, tahtayı kurar.
4. **Tile tıklama:** `BoardTileCollectCoordinator` — hedefi hesaplar, uçuş animasyonunu başlatır, session'a collect uygular.
5. **HUD güncelleme:** Session event yayınlar → `OrderRackHudController` → presenter'lar ikonları ve rack'i ekrana yansıtır.

---

## Collect (tile toplama)

Tile tıklanınca sıra: **rezerve et → uçur → commit et → gerekirse rack'ten order'a aktar**.

Birden fazla tile aynı anda uçabilir. Her uçuş gitmeden önce hedef slotunu **rezerve** eder; böylece iki tile aynı order ikonuna veya aynı rack slotuna yazılmaz. "Rack dolu mu?" sorusu sadece yerleşmiş tile'lara değil, **havada uçan** tile'lara da bakılarak cevaplanır (projeksiyon).

```
Tile tıklandı
   │
   ▼
session.TryReserveCollect(kind)
   │
   ├─ rezerve OK → tile uçuş animasyonu → CommitReservation → TryCollectTile
   │                 │
   │                 └─ order tamamlandıysa → rack'teki uygun tile'lar order'a aktarılır (animasyonlu)
   │
   └─ rezerve FAIL (projeksiyon rack dolu)
          → tıklama FIFO buffer'a alınır; tile tahtada kalır
          → bir uçuş inince veya aktarım slot boşaltınca tekrar denenir
```

| Sınıf | Ne yapar? | Katman |
|-------|-----------|--------|
| `CollectReservationService` | Uçuş başlamadan hedef slotunu ayırır | Gameplay |
| `BoardTileCollectCoordinator` | Eşzamanlı uçuşları ve bekleyen tıklamaları yönetir | Presentation |
| `RackDrainService` | Rack'teki tile'ı uygun order ikonuna taşır | Gameplay |
| `TileCollectFly` | Tile'ın ekranda uçma animasyonu | Presentation |

**Kabul edilen sınır:** Rezervasyon sistemi "bir order bitti, yeni müşteri geldi" senaryosunu önceden tahmin etmez. Çok nadir olarak, order'ı bitiren tile hâlâ uçarken gelen başka bir tıklama yeni müşteriye değil rack'e gidebilir. Rack asla taşmaz.

---

## Order ve rack mantığı (Gameplay)

Bu bölüm **oyunun beyni** — ekran yok, sadece kurallar ve sayaçlar.

### Kavramlar

| Kavram | Anlamı |
|--------|--------|
| **Order** | Bir müşterinin istediği tile dizisi (ör. kırmızı, mavi, sarı). Ekranda bir satır ikon olarak görünür. |
| **Order slot** | Ekranda aynı anda görünen müşteri satırı sayısı (varsayılan 2). Level'da daha fazla müşteri varsa kuyrukta bekler. |
| **Rack** | Order'a uymayan tile'ların geçici depolandığı yer. Kapasite dolunca yeni tile alınamaz → fail. |
| **Aktarım** | Bir order tamamlanınca rack'teki tile'lardan uygun olanlar otomatik olarak açık order'lara gider. |

### Akış özeti

```
Tile toplandı
   │
   ├─ Bu kind, bir order satırında boş ikon mu? ──evet──► order ikonu dolar
   │                                                      │
   │                                                      └─ tüm ikonlar doldu mu? → müşteri biter, sıradaki gelir
   │
   └─ hayır ──► rack'e eklen (doluysa fail)
```

### Sınıflar ve görevleri

| Sınıf | Görevi (yeni başlayan için) |
|-------|------------------------------|
| `LevelObjectiveSession` | Dışarıya tek kapı: "tile topla", "rack slotu ne?", "order satırı ne?" — içeride alt sistemlere dağıtır |
| `ActiveOrderSlots` | Hangi müşteri hangi satırda, hangi ikonlar doldu, sırada kim var |
| `RackState` | Rack'te hangi tile'lar var, kaç slot dolu |
| `RackDrainService` | Order bitince rack'i tarar; eşleşen tile'ı order ikonuna taşır |
| `CollectPipeline` | Tek bir tile için: önce order'a bak, olmazsa rack'e koy kuralını çalıştırır |
| `CollectReservationService` | Uçuş sırasında "bu slot benim" rezervasyonu (collect ile birlikte çalışır) |

### Gameplay ile HUD nasıl konuşur?

HUD ekranı çizmek ister; gameplay kuralları çalıştırır. Arada **`IObjectiveHudState`** var: salt okunur bir özet ("2. satırda şu ikonlar dolu, rack'in 3. slotunda mavi tile var"). Presenter'lar session'ın `TryCollectTile` gibi yazma metodlarına erişmez — sadece bu özeti okuyup Image'lara sprite koyar.

`ObjectiveLayoutSpec` ise rack kapasitesi ve kaç order satırı olacağını sayı olarak taşır; prefab veya Unity'den bağımsızdır. `HudLayoutConfig` asset'i hem UI builder'a hem level loader'a aynı sayıları verir.

---

## Order / Rack HUD (Presentation)

Oyuncunun gördüğü sipariş satırları ve rack çubuğu. Kurallar burada **yok**; yalnızca `IObjectiveHudState` okunur ve çizilir.

```
ObjectiveHud (sahne)
  ├─ OrderRackHudBuilder     → prefab spawn (Awake'te)
  └─ OrderRackHud            → ince facade; session bağlar
         │
         ▼
  OrderRackHudController      → gameplay event'lerini dinler, refresh koordine eder
    ├─ OrderPresenter[]       → her müşteri satırı
    ├─ RackPresenter          → rack slot görselleri
    └─ HudDestinationLayout   → uçan tile nereye gidecek? (Rect hedefi)
         │
         ▼
  OrderView / OrderSlotView / RackView / RackSlotView   (prefab'lar)
```

**Prefab yapısı (kısa):**

```
Order.prefab          → bir müşteri satırı (içinde runtime'da OrderSlot x N)
OrderSlot.prefab      → tek ikon + tik işareti
Rack.prefab           → rack çerçevesi (içinde runtime'da RackSlot x N)
RackSlot.prefab       → arka plan + tile ikonu
```

| Referans | Nerede? | Ne işe yarar? |
|----------|---------|---------------|
| `orderContainer` / `rackContainer` | Sahnedeki boş RectTransform | Builder prefab'ı *nereye* koyacağını bilir |
| `container` / `slotContainer` | Order/Rack prefab'ının içinde | Spawn edilen slot'ların *parent'ı* |

### HudLayoutConfig

Tek ayar dosyası: `Assets/GameData/HudLayoutConfig.asset`

| Alan | Etkisi |
|------|--------|
| `rackCapacity` | Kaç rack slotu (hem UI hem gameplay) |
| `activeOrderSlotCount` | Kaç order satırı (hem UI hem gameplay) |
| Prefab referansları | Hangi şablonlar spawn edilecek |

Slot görünümünü değiştirmek için prefab'ı düzenle; sayıları değiştirmek için config asset'ini düzenle.

---

## Tile davranış sistemi (genişletilebilir tile'lar)

Şu an oyunda fiilen tek tip tile var: **standard** (her şeye izin verir). Altyapı ise farklı tile tipleri (kilitli, buzlu vb.) eklemeye hazır.

Her hücre `BoardCell`: renk (`TileKind`) + `BehaviorId`. Kurallar `ITileBehavior` sınıflarında; hangi id hangi davranışa gider `TileBehaviorCatalog` söyler (`GameCompositionRoot`'ta kurulur). Görsel (overlay, renk) ayrı asset'te: `TileBehaviorRegistry`.

JSON'da opsiyonel `behaviors` matrisi (`matrix3D` ile aynı boyut). Yoksa her tile `standard` sayılır:

```json
{
  "width": 3, "height": 1, "depth": 1,
  "matrix3D": [[[0, 1, 2]]],
  "behaviors": [[["standard", "ice", "standard"]]],
  "orders": [[0, 1, 2]]
}
```

Yeni davranış eklemek:

| Adım | Ne yap |
|------|--------|
| Kural | `ITileBehavior` implement et (tıklanabilirlik, tahtadan kalkma vb.) |
| Collect özelleştir | Ayrıca `ITileCollectContributor` ekle |
| Kayıt | `GameCompositionRoot.BuildBehaviorCatalog()` dizisine ekle |
| Level | JSON `behaviors` matrisine id yaz |
| Görsel | `TileBehaviorRegistry` asset'ine overlay/tint ekle |

> Level'da tahta tile sayısı = order ikon toplamı olmalıdır (parser kontrol eder). Tahtadan tile kaldıran davranışlar bu dengeyi bozabilir.

---

## Level Editor

Unity menüsü: **Window → Tile Level Editor**. Runtime oyun kodunu değiştirmez; JSON level üretir.

**İki faz:**

1. **Order Authoring** — Palette'den tile ekleyerek müşteri sipariş sütunlarını oluştur → **Finalize orders** ile kilitle.
2. **Placing Tiles** — Sıradaki tile'ları tahtaya yerleştir (rack araç olarak kullanılabilir) → **Validate** → **Export**.

Önemli kurallar:

- **Finalize** olmadan export edilmez; export finalize anındaki sipariş snapshot'ını yazar.
- Export öncesi el (hand) ve rack boş olmalı.
- **Edit orders** ile finalize sonrası sipariş düzenlenebilir; bu modda Place/Remove kapalıdır.

Her tahta tile'ı hangi siparişten geldiğini hatırlar (**provenance**). Remove ile kaldırılan tile doğru order sütununa döner.

```
TileLevelEditorWindow
  ├─ LevelEditorOrderModel   → sipariş sütunları, finalize snapshot
  ├─ LevelEditorBoardModel   → 3D grid, rack, provenance
  └─ EditorTileIcons         → palette çizimi
```

---

## Sık değişiklikler — nereye dokunmalı?

| İstediğin | Dosya / yer |
|-----------|-------------|
| Rack 9 slot, 3 order satırı | `HudLayoutConfig` asset |
| Slot görünümü (ikon, tik, çerçeve) | `OrderSlot.prefab` / `RackSlot.prefab` |
| Order tamamlanınca animasyon | `OrderRackHud` Inspector (`orderCompleteScale*`) |
| Tile uçuş hızı / easing | `TileCollectFly` |
| Eşzamanlı tıklama / buffer | `BoardTileCollectCoordinator` |
| Rezervasyon / projeksiyon | `CollectReservationService` |
| Order mı rack mi kuralı | `CollectPipeline`, `MatchOrRackCollectHandler` |
| Yeni level | Level Editor → Export |
| Yeni tile davranışı | `ITileBehavior` + katalog + registry |

---

## Terimler sözlüğü

| Terim | Kısa açıklama |
|-------|----------------|
| Collect | Tile'ı tahtadan alıp order veya rack'e gönderme |
| Session | Bir level oturumu; order, rack ve collect state'i `LevelObjectiveSession`'da |
| Presenter | Gameplay state → UI komutları çevirici (`OrderPresenter`, `RackPresenter`) |
| View | Ham UI objesi (prefab / MonoBehaviour) |
| Projeksiyon | Commit edilmiş state + havada uçan rezervasyonların toplamı |
| Aktarım | Rack'teki tile'ın uygun order ikonuna otomatik gitmesi |
| Facade | Dışarıya sade API sunan ince sınıf (`LevelObjectiveSession`, `OrderRackHud`) |
