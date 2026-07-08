using UnityEngine;

namespace Core
{
    [CreateAssetMenu(fileName = "TileBehaviorDefinition", menuName = "Tile Jam/Tile Behavior Definition")]
    public sealed class TileBehaviorDefinition : ScriptableObject
    {
        [Tooltip("Stable id referenced by BoardCell.BehaviorId (e.g. standard, locked, ice).")]
        public string id = "standard";

        [Header("Visuals (optional)")]
        [Tooltip("Overlay sprite drawn on top of the tile icon (e.g. lock, ice). Leave empty for none.")]
        public Sprite overlaySprite;

        [Tooltip("Multiplied with the tile's base colors so a behavior reads at a glance even without an overlay sprite.")]
        public Color tintColor = Color.white;
    }
}
