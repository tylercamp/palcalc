using PalCalc.Solver.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver.Processing.Search
{
    /// <summary>
    /// Lazily pairs every item from one input list with every item from another
    /// and divides those pairs into batches without constructing the full
    /// Cartesian product in memory.
    /// </summary>
    public interface ILazyCartesianProduct<T>
    {
        long Count { get; }
        IEnumerable<IEnumerable<(T, T)>> Chunks(int chunkSize);

        /// <summary>
        /// Creates a new ILazyCartesianProduct containing pairs where both items
        /// satisfy `predicate`.
        /// </summary>
        ILazyCartesianProduct<T> Where(Func<T, bool> predicate, CancellationToken token);
    }

    /// <summary>
    /// Enumerates pairs in ordinary row-major order: each item from the second
    /// list is paired with the first item before advancing through the first
    /// list.
    /// </summary>
    public class LazyCartesianProduct<T>(List<T> listA, List<T> listB) : ILazyCartesianProduct<T>
    {
        public long Count { get; } = ((long)listA.Count) * ((long)listB.Count);

        private IEnumerable<(T, T)> ChunkAt(long chunkStart, long chunkEnd)
        {
            int aStartIndex = (int)(chunkStart / listB.Count);
            int bStartIndex = (int)(chunkStart % listB.Count);

            int aEndIndex = (int)(chunkEnd / listB.Count);
            int bEndIndex = (int)(chunkEnd % listB.Count);

            if (aStartIndex == aEndIndex)
            {
                var elemA = listA[aStartIndex];
                for (int i = bStartIndex; i <= bEndIndex; i++)
                    yield return (elemA, listB[i]);
            }
            else
            {
                for (int ia = aStartIndex; ia <= aEndIndex; ia++)
                {
                    var elemA = listA[ia];

                    int ibStart = ia == aStartIndex ? bStartIndex : 0;
                    int ibEnd = ia == aEndIndex ? bEndIndex : (listB.Count - 1);

                    for (int ib = ibStart; ib <= ibEnd; ib++)
                        yield return (elemA, listB[ib]);
                }
            }
        }

        public IEnumerable<IEnumerable<(T, T)>> Chunks(int chunkSize)
        {
            long curChunkStart = 0;
            while (curChunkStart < Count)
            {
                var curChunkEnd = Math.Min(Count - 1, curChunkStart + chunkSize);

                yield return ChunkAt(curChunkStart, curChunkEnd);

                curChunkStart = curChunkEnd + 1;
            }
        }

        public ILazyCartesianProduct<T> Where(Func<T, bool> predicate, CancellationToken token) =>
            new LazyCartesianProduct<T>(
                listA.Where(predicate).TakeUntilCancelled(token).ToList(),
                listB.Where(predicate).TakeUntilCancelled(token).ToList()
            );
    }

    public class ConcatenatedLazyCartesianProduct<T> : ILazyCartesianProduct<T>
    {
        private List<ILazyCartesianProduct<T>> innerProducts;

        public ConcatenatedLazyCartesianProduct(IEnumerable<(List<T>, List<T>)> setPairs)
        {
            innerProducts = setPairs.Select(p => (ILazyCartesianProduct<T>)new AntiDiagonalLazyCartesianProduct<T>(p.Item1, p.Item2)).ToList();
        }

        public ConcatenatedLazyCartesianProduct(IEnumerable<ILazyCartesianProduct<T>> products)
        {
            innerProducts = products.ToList();
        }

        public long Count => innerProducts.Sum(p => p.Count);

        public IEnumerable<IEnumerable<(T, T)>> Chunks(int chunkSize)
        {
            foreach (var p in innerProducts)
                foreach (var chunk in p.Chunks(chunkSize))
                    yield return chunk;
        }

        public ILazyCartesianProduct<T> Where(Func<T, bool> predicate, CancellationToken token) =>
            new ConcatenatedLazyCartesianProduct<T>(innerProducts.Select(ip => ip.Where(predicate, token)).ToList());
    }
}
