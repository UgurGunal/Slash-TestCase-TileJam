# Tile Jam

Unity üzerinde geliştirilmiş bir tile match / order completion oyunu.  
Tahtadaki taşlara tıkla, müşteri siparişlerini tamamla; rack dolmadan level'ı bitir.

---

## Oyun

Tahtadaki tile'lara tıklayarak onları üstteki siparişlere gönderirsin.

- Siparişteki tüm ikonlar dolunca müşteri tamamlanır, sıradaki gelir.
- Uygun sipariş yoksa tile geçici depoya (**rack**) gider.
- Rack dolarsa level kaybedilir.

---

## Ekran görüntüleri

<!-- Görsellerini buraya ekle. Örnek: -->
<!-- ![Gameplay](Docs/Screenshots/gameplay.png) -->
<!-- ![Level Editor](Docs/Screenshots/level-editor.png) -->

| Gameplay | Level Editor |
|:--------:|:------------:|
| *görsel ekle* | *görsel ekle* |

---

## Teknik

| | |
|---|---|
| **Engine** | Unity 6 (`6000.0.62f1`) |
| **Render** | URP 2D |
| **Animasyon** | DOTween |

Kod katmanlı assembly yapısında:

`Core` → `LevelData` → `Gameplay` → `Presentation` → `LevelEditor`

Gameplay kuralları Unity'den bağımsız saf C#; level verisi JSON üzerinden yüklenir. Detaylı mimari için: [`Docs/Proje-Mimarisi.md`](Docs/Proje-Mimarisi.md)

---

## Nasıl çalıştırılır

1. Unity Hub ile projeyi aç (Unity **6000.0.62f1** veya uyumlu sürüm).
2. `Assets/Scenes/Main.unity` sahnesini aç.
3. Play'e bas.

Level'lar `Assets/Resources/Levels/` altında JSON olarak durur.  
Özel level yazmak için Unity menüsünden Level Editor penceresini kullanabilirsin.

---

## Proje yapısı

```
Assets/Scripts/
├── Core/           # TileKind, sabitler
├── LevelData/      # JSON parse, board şeması
├── Gameplay/       # Order / rack kuralları
├── Presentation/   # UI, tahta, animasyon
└── LevelEditor/    # Editör aracı
```
