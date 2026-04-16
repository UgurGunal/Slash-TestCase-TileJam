namespace LevelData
{
    /// <summary>Validated board plus order list from one level JSON.</summary>
    public sealed class LevelDefinition
    {
        public LevelDefinition(LevelBoardSpec board, LevelOrdersSpec orders)
        {
            Board = board;
            Orders = orders;
        }

        public LevelBoardSpec Board { get; }
        public LevelOrdersSpec Orders { get; }
    }
}
