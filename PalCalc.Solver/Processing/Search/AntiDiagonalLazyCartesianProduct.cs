using PalCalc.Solver.Utils;

namespace PalCalc.Solver.Processing.Search
{
    /// <summary>
    /// Prioritizes pairs near the beginning of both input lists instead of
    /// exhausting the second list for each item in the first.
    ///
    /// `SearchFrontier` orders parents by breeding effort. Visiting efficient
    /// parents from both lists early tends to discover efficient children
    /// sooner, allowing `CandidateExpander` to skip pending pairs whose parents
    /// have since been marked as outdated.
    ///
    /// The product is divided into roughly square tiles and the tiles are
    /// emitted along anti-diagonals to maintain that priority.
    /// </summary>
    public class AntiDiagonalLazyCartesianProduct<T>(List<T> listA, List<T> listB) : ILazyCartesianProduct<T>
    {
        public long Count { get; } = ((long)listA.Count) * listB.Count;

        private IEnumerable<(T, T)> TileAt(
            int aStart,
            int aEnd,
            int bStart,
            int bEnd)
        {
            for (int ia = aStart; ia < aEnd; ia++)
            {
                var elemA = listA[ia];

                for (int ib = bStart; ib < bEnd; ib++)
                    yield return (elemA, listB[ib]);
            }
        }

        public IEnumerable<IEnumerable<(T, T)>> Chunks(int chunkSize)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);

            if (listA.Count == 0 || listB.Count == 0)
                yield break;

            // Prefer square tiles so each chunk stays near the beginning of both
            // lists. Expand along the other axis when one list is too narrow to
            // use the requested chunk capacity.
            int tileASize = Math.Min(
                listA.Count,
                Math.Max(1, (int)Math.Sqrt(chunkSize))
            );
            int tileBSize = Math.Min(
                listB.Count,
                Math.Max(1, chunkSize / tileASize)
            );
            tileASize = Math.Min(
                listA.Count,
                Math.Max(1, chunkSize / tileBSize)
            );

            int numATiles = 1 + (listA.Count - 1) / tileASize;
            int numBTiles = 1 + (listB.Count - 1) / tileBSize;

            for (int diagonal = 0; diagonal < numATiles + numBTiles - 1; diagonal++)
            {
                int firstATile = Math.Max(0, diagonal - (numBTiles - 1));
                int lastATile = Math.Min(numATiles - 1, diagonal);

                for (int aTile = firstATile; aTile <= lastATile; aTile++)
                {
                    int bTile = diagonal - aTile;

                    int aStart = aTile * tileASize;
                    int bStart = bTile * tileBSize;

                    yield return TileAt(
                        aStart,
                        Math.Min(aStart + tileASize, listA.Count),
                        bStart,
                        Math.Min(bStart + tileBSize, listB.Count)
                    );
                }
            }
        }

        public ILazyCartesianProduct<T> Where(Func<T, bool> predicate, CancellationToken token) =>
            new AntiDiagonalLazyCartesianProduct<T>(
                listA.Where(predicate).TakeUntilCancelled(token).ToList(),
                listB.Where(predicate).TakeUntilCancelled(token).ToList()
            );
    }
}
