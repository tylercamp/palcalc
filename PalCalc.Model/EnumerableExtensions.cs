using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PalCalc.Model
{
    public static class EnumerableExtensions
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

        public static IEnumerable<IEnumerable<T>> Batched<T>(this List<T> elements, int batchSize)
        {
            for (int i = 0; i < elements.Count / batchSize + 1 && elements.Count > 0; i++)
            {
                yield return elements.Skip(i * batchSize).Take(batchSize);
            }
        }

        public static IEnumerable<IEnumerable<T>> BatchedForParallel<T>(this List<T> elements) => elements.Batched((elements.Count / Environment.ProcessorCount) + 1);

        public static IEnumerable<T> SkipNull<T>(this IEnumerable<T> elements) => elements.Where(v => v != null);

        public static V GetValueOrElse<K, V>(this IDictionary<K, V> dict, K key, V fallback)
        {
            if (dict.ContainsKey(key)) return dict[key];
            else return fallback;
        }

        public static V GetValueFromAny<K, V>(this IDictionary<K, V> dict, params K[] keys) => dict[keys.First(dict.ContainsKey)];

        public static int SetHash<T>(this IEnumerable<T> elements)
        {
            var baseHash = 0;
            var total = 0;
            foreach (var g in elements.GroupBy(e => e.GetHashCode()).OrderBy(g => g.Key))
            {
                var count = g.Count();
                baseHash = HashCode.Combine(baseHash, g.Key, count);
                total += count;
            }

            return HashCode.Combine(baseHash, total);
        }

        public static T MostCommonOrDefault<T>(this IEnumerable<T> e) => e.GroupBy(v => v).OrderByDescending(g => g.Key).Select(g => g.Key).FirstOrDefault();

        public static IEnumerable<(T, int)> ZipWithIndex<T>(this IEnumerable<T> e) => e.Zip(Enumerable.Range(0, e.Count()));

        public static IEnumerable<T> Tap<T>(this IEnumerable<T> e, Action<T> action)
        {
            foreach (var r in e)
            {
                action(r);
                yield return r;
            }
        }
    }
}
