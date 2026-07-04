namespace Gameplay
{
    public interface ICollectFlowLogger
    {
        bool IsEnabled { get; }
        void Log(string message);
    }

    public sealed class NullCollectFlowLogger : ICollectFlowLogger
    {
        public static readonly NullCollectFlowLogger Instance = new NullCollectFlowLogger();

        public bool IsEnabled => false;

        public void Log(string message) { }
    }
}
