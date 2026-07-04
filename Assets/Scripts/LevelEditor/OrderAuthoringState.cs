using System;
using System.Collections.Generic;
using Core;

namespace LevelEditor
{
    /// <summary>Finalize/lock state and immutable order snapshot for export validation.</summary>
    public sealed class OrderAuthoringState
    {
        List<List<TileKind>> _snapshotAtFinalize;
        bool _finalized;

        public bool IsFinalized => _finalized;

        public IReadOnlyList<IReadOnlyList<TileKind>> SnapshotAtFinalize =>
            _snapshotAtFinalize != null ? new SnapshotView(_snapshotAtFinalize) : Array.Empty<IReadOnlyList<TileKind>>();

        public void FinalizeFrom(List<List<TileKind>> liveColumns)
        {
            if (_finalized) return;
            _finalized = true;
            CaptureSnapshot(liveColumns);
        }

        public void Clear()
        {
            _finalized = false;
            _snapshotAtFinalize = null;
        }

        public void Unlock()
        {
            _finalized = false;
            _snapshotAtFinalize = null;
        }

        void CaptureSnapshot(List<List<TileKind>> liveColumns)
        {
            _snapshotAtFinalize = new List<List<TileKind>>();
            if (liveColumns == null) return;
            for (var i = 0; i < liveColumns.Count; i++)
            {
                var col = liveColumns[i];
                _snapshotAtFinalize.Add(col != null ? new List<TileKind>(col) : new List<TileKind>());
            }
        }

        public bool TryValidateSnapshot(out string error)
        {
            error = null;
            if (_snapshotAtFinalize == null)
            {
                error = "No finalized order snapshot. Finalize orders again.";
                return false;
            }

            var total = 0;
            for (var c = 0; c < _snapshotAtFinalize.Count; c++)
            {
                var col = _snapshotAtFinalize[c];
                if (col == null) continue;
                total += col.Count;
            }

            if (total < 1)
            {
                error = "Finalized orders snapshot is empty.";
                return false;
            }

            return true;
        }

        sealed class SnapshotView : IReadOnlyList<IReadOnlyList<TileKind>>
        {
            readonly List<List<TileKind>> _inner;

            public SnapshotView(List<List<TileKind>> inner) => _inner = inner;

            public IReadOnlyList<TileKind> this[int index] => _inner[index];
            public int Count => _inner.Count;
            public IEnumerator<IReadOnlyList<TileKind>> GetEnumerator()
            {
                for (var i = 0; i < _inner.Count; i++)
                    yield return _inner[i];
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
