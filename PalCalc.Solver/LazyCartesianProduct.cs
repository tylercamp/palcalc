﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Solver
{
    public interface ILazyCartesianProduct<T>
    {
        long Count { get; }
        IEnumerable<IEnumerable<(T, T)>> Chunks(int chunkSize);
    }

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
    }

    public class ConcatenatedLazyCartesianProduct<T>(IEnumerable<(List<T>, List<T>)> setPairs) : ILazyCartesianProduct<T>
    {
        private List<LazyCartesianProduct<T>> innerProducts = setPairs.Select(p => new LazyCartesianProduct<T>(p.Item1, p.Item2)).ToList();

        public long Count => innerProducts.Sum(p => p.Count);

        public IEnumerable<IEnumerable<(T, T)>> Chunks(int chunkSize)
        {
            foreach (var p in innerProducts)
                foreach (var chunk in p.Chunks(chunkSize))
                    yield return chunk;
        }
    }
}
