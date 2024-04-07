using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc
{
    static class EnumerableExtensions
    {
        // thanks chatgpt
        // Returns the list of combinations of elements in the given list, where combinations are order-independent
        public static IEnumerable<IEnumerable<T>> Combinations<T>(this List<T> elements, int maxSubListSize)
        {
            for (int i = 0; i <= maxSubListSize; i++)
            {
                foreach (var combo in GenerateCombinations(elements, i))
                {
                    yield return combo;
                }
            }
        }

        private static IEnumerable<IEnumerable<T>> GenerateCombinations<T>(List<T> list, int combinationSize)
        {
            if (combinationSize == 0)
            {
                yield return new T[0];
            }
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    foreach (var next in GenerateCombinations(list.Skip(i + 1).ToList(), combinationSize - 1))
                    {
                        yield return new T[] { list[i] }.Concat(next);
                    }
                }
            }
        }





        public static IEnumerable<IEnumerable<T>> PermutationsUpToN<T>(this List<T> elements, int maxResultListSize)
        {
            return GeneratePermutationsWithRepetitionInternal(elements, new List<T>(), maxResultListSize);
        }

        private static IEnumerable<IEnumerable<T>> GeneratePermutationsWithRepetitionInternal<T>(List<T> elements, List<T> current, int maxResultListSize)
        {
            if (current.Count > 0)
            {
                yield return current;
            }

            if (current.Count == maxResultListSize)
            {
                yield break;
            }

            foreach (T element in elements)
            {
                var newList = new List<T>(current) { element };
                foreach (var permutation in GeneratePermutationsWithRepetitionInternal(elements, newList, maxResultListSize))
                {
                    yield return permutation;
                }
            }
        }
    }
}
