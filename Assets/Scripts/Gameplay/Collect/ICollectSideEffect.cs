using Core;

namespace Gameplay.Collect
{
    /// <summary>Extension point for VFX, audio, analytics after a collect step (no default implementations).</summary>
    public interface ICollectSideEffect
    {
        void OnCollectResult(TileCollectResult result, TileKind kind);
    }
}
