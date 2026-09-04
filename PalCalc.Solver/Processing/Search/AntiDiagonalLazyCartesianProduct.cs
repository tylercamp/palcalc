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
    public class AntiDiagonalLazyCartesianProduct<T> : ILazyCartesianProduct<T>
    {
        private readonly List<T> listA;
        private readonly List<T> listB;
        private readonly bool unorderedSameList;

        public AntiDiagonalLazyCartesianProduct(List<T> listA, List<T> listB)
            : this(listA, listB, unorderedSameList: false)
        {
        }

        private AntiDiagonalLazyCartesianProduct(
            List<T> listA,
            List<T> listB,
            bool unorderedSameList
        )
        {
            this.listA = listA;
            this.listB = listB;
            this.unorderedSameList = unorderedSameList;
            Count = unorderedSameList
                ? ((long)listA.Count * (listA.Count + 1)) / 2
                : ((long)listA.Count) * listB.Count;
        }

        public static AntiDiagonalLazyCartesianProduct<T> Unordered(List<T> list) =>
            new(list, list, unorderedSameList: true);

        public long Count { get; }

        private IEnumerable<(T, T)> TileAt(
            int aStart,
            int aEnd,
            int bStart,
            int bEnd)
        {
            for (int ia = aStart; ia < aEnd; ia++)
            {
                var elemA = listA[ia];

                for (
                    int ib = unorderedSameList ? Math.Max(bStart, ia) : bStart;
                    ib < bEnd;
                    ib++
                )
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

                    int aEnd = Math.Min(aStart + tileASize, listA.Count);
                    int bEnd = Math.Min(bStart + tileBSize, listB.Count);
                    if (!unorderedSameList || bEnd > aStart)
                        yield return TileAt(aStart, aEnd, bStart, bEnd);
                }
            }
        }

        public ILazyCartesianProduct<T> Where(Func<T, bool> predicate, CancellationToken token)
        {
            if (unorderedSameList)
            {
                var filtered = listA
                    .Where(predicate)
                    .TakeUntilCancelled(token)
                    .ToList();
                return Unordered(filtered);
            }

            return new AntiDiagonalLazyCartesianProduct<T>(
                listA.Where(predicate).TakeUntilCancelled(token).ToList(),
                listB.Where(predicate).TakeUntilCancelled(token).ToList()
            );
        }
    }
}
