using UnityEngine;

namespace Presentation
{
    public sealed class UnityCollectFlowLogger : Gameplay.ICollectFlowLogger
    {
        public bool IsEnabled { get; set; }

        public void Log(string message)
        {
            if (IsEnabled)
                Debug.Log(message);
        }
    }
}
