using Core;

namespace Gameplay
{
    /// <summary>
    /// Read-only order/rack snapshot for HUD projection. Keeps presentation off the mutable session API.
    /// </summary>
    public interface IObjectiveHudState
    {
        int MaxOrderIconsOnLevel { get; }

        TileKind? GetRackSlot(int rackSlotIndex);

        bool TryGetOrderRow(int hudSlotIndex, out ObjectiveHudRowView row);

        bool IsOrderSlotIdle(int hudSlotIndex);
    }
}
