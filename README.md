# Tile Jam

<p align="center">
  <img src="Docs/ScreenShots/Image%20Sequence_001_0000.png" width="220" alt="Screenshot 1" />
  <img src="Docs/ScreenShots/Image%20Sequence_002_0000.png" width="220" alt="Screenshot 2" />
  <img src="Docs/ScreenShots/Image%20Sequence_003_0000.png" width="220" alt="Screenshot 3" />
  <img src="Docs/ScreenShots/Image%20Sequence_004_0000.png" width="220" alt="Screenshot 4" />
  <img src="Docs/ScreenShots/Image%20Sequence_005_0000.png" width="220" alt="Screenshot 5" />
</p>

A tile match / order completion game built in Unity.  
Tap tiles on the board, fulfill customer orders, and finish the level before the rack fills up.

---

## How to play

Tap tiles on the board to send them to the orders above.

- When every icon in an order is filled, that customer is complete and the next one arrives.
- If no matching order is available, the tile goes to the temporary storage (**rack**).
- If the rack fills up, you lose the level.

---

## Tech

| | |
|---|---|
| **Engine** | Unity 6 (`6000.0.62f1`) |
| **Render** | URP 2D |
| **Animation** | DOTween |

Code is split into layered assemblies:

`Core` → `LevelData` → `Gameplay` → `Presentation` → `LevelEditor`

Gameplay rules are plain C# with no Unity dependency; levels load from JSON. For architecture details, see [`Docs/Proje-Mimarisi.md`](Docs/Proje-Mimarisi.md).

---

## Getting started

1. Open the project in Unity Hub (Unity **6000.0.62f1** or a compatible version).
2. Open `Assets/Scenes/Main.unity`.
3. Hit Play.

Levels live under `Assets/Resources/Levels/` as JSON.  
Use the Level Editor window in Unity to author custom levels.

---

## Project structure

```
Assets/Scripts/
├── Core/           # TileKind, constants
├── LevelData/      # JSON parsing, board schema
├── Gameplay/       # Order / rack rules
├── Presentation/   # UI, board, animation
└── LevelEditor/    # Editor tool
```
