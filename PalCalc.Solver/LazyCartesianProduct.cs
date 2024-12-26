using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public interface ILazyCartesianProduct<T>
    {
        int Count { get; }
        IEnumerable<IEnumerable<(T, T)>> Chunks(int chunkSize);
    }

    public class LazyCartesianProduct<T>(List<T> listA, List<T> listB) : ILazyCartesianProduct<T>
    {
        private IEnumerable<T> SubList(List<T> l, int start, int end)
        {
            for (int i = start; i <= end; i++)
                yield return l[i];
        }

        public int Count { get; } = listA.Count * listB.Count;

        public IEnumerable<IEnumerable<(T, T)>> Chunks(int chunkSize)
        {
            int curChunkStart = 0;
            while (curChunkStart < Count)
            {
                var curChunkEnd = Math.Min(Count - 1, curChunkStart + chunkSize);

                IEnumerable<(T, T)> chunk = [];

                int aStartIndex = curChunkStart / listB.Count;
                int bStartIndex = curChunkStart % listB.Count;

                int aEndIndex = (curChunkEnd / listB.Count);
                int bEndIndex = (curChunkEnd % listB.Count);

                if (aStartIndex == aEndIndex)
                {
                    var elemA = listA[aStartIndex];
                    chunk = chunk.Concat(SubList(listB, bStartIndex, bEndIndex).Select(elemB => (elemA, elemB)));
                }
                else
                {
                    for (int ia = aStartIndex; ia <= aEndIndex; ia++)
                    {
                        var elemA = listA[ia];
                        if (ia == aStartIndex)
                        {
                            chunk = chunk.Concat(listB.Skip(bStartIndex).Select(elemB => (elemA, elemB)));
                        }
                        else if (ia == aEndIndex)
                        {
                            chunk = chunk.Concat(SubList(listB, 0, bEndIndex).Select(elemB => (elemA, elemB)));
                        }
                        else
                        {
                            chunk = chunk.Concat(listB.Select(elemB => (elemA, elemB)));
                        }
                    }
                }

                yield return chunk;

                curChunkStart = curChunkEnd + 1;
            }
        }
    }

    public class ConcatenatedLazyCartesianProduct<T>(IEnumerable<(List<T>, List<T>)> setPairs) : ILazyCartesianProduct<T>
    {
        private List<LazyCartesianProduct<T>> innerProducts = setPairs.Select(p => new LazyCartesianProduct<T>(p.Item1, p.Item2)).ToList();

        public int Count => innerProducts.Sum(p => p.Count);

        public IEnumerable<IEnumerable<(T, T)>> Chunks(int chunkSize)
        {
            foreach (var p in innerProducts)
                foreach (var chunk in p.Chunks(chunkSize))
                    yield return chunk;
        }
    }
}
