namespace PalCalc.Solver
{
    // ty chatgpt

    // `BreedingBatchSolver` includes an early "IsOutdated" check which gets updated as new optimal
    // pals are discovered. We want to process the high-efficiency parent pairs first to produce
    // high-efficiency children, which should let us quickly rule out the low-efficiency remaining
    // in the set.
    //
    // `SearchFrontier` orders the lists of parents by effort, and this `AntiDiagonal` approach ensures
    // we visit the early inner products first.

    /// <summary>
    /// Lazily enumerates a Cartesian product in approximately increasing order of
    /// the combined indices of each pair.
    ///
    /// The product is divided into roughly square tiles. Tiles are emitted along
    /// anti-diagonals so that pairs near the beginning of both input lists are
    /// scheduled before pairs with a large index in either list.
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
