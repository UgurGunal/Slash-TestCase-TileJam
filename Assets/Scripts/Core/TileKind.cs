namespace Core
{
    /// <summary>
    /// Playable icons: <see cref="Type0"/>–<see cref="Type14"/>. <see cref="None"/> = no tile (e.g. failed <c>TryGet</c>); empty board cells use <c>null</c> in level data, not <see cref="None"/>.
    /// </summary>
    public enum TileKind
    {
        None = -1,
        Type0 = 0,
        Type1 = 1,
        Type2 = 2,
        Type3 = 3,
        Type4 = 4,
        Type5 = 5,
        Type6 = 6,
        Type7 = 7,
        Type8 = 8,
        Type9 = 9,
        Type10 = 10,
        Type11 = 11,
        Type12 = 12,
        Type13 = 13,
        Type14 = 14,
    }
}
