using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

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

        public static int PreferredParallelBatchSize(this long collectionSize) =>
            (int)Math.Min(
                100000,
                (collectionSize / Environment.ProcessorCount) + 1
            );

        public static int PreferredParallelBatchSize<T>(this List<T> elements) =>
            ((long)elements.Count).PreferredParallelBatchSize();

        public static IEnumerable<IEnumerable<T>> Batched<T>(this List<T> elements, int batchSize)
        {
            for (int i = 0; i < elements.Count / batchSize + 1 && elements.Count > 0; i++)
            {
                yield return elements.Skip(i * batchSize).Take(batchSize);
            }
        }

        public static IEnumerable<IEnumerable<T>> BatchedForParallel<T>(this List<T> elements) =>
            elements.Batched(elements.PreferredParallelBatchSize());

        public static IEnumerable<T> SkipNull<T>(this IEnumerable<T> elements) => elements.Where(v => v != null);

        public static V GetValueOrElse<K, V>(this IDictionary<K, V> dict, K key, V fallback)
        {
            if (dict.ContainsKey(key)) return dict[key];
            else return fallback;
        }

        public static V GetValueFromAny<K, V>(this IDictionary<K, V> dict, params K[] keys) => dict[keys.First(dict.ContainsKey)];

        public static int _SetHash<T>(this IEnumerable<T> elements)
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

        public static int SetHash<T>(this IEnumerable<T> elements)
        {
            const int MaxSize = 8;

            // Stack allocation: no heap allocations here
            Span<int> buffer = stackalloc int[MaxSize];
            int count = 0;

            // Read hash codes into the buffer
            foreach (var element in elements)
            {
                if (count == MaxSize)
                    throw new InvalidOperationException("Too many elements in the set.");

                buffer[count++] = element?.GetHashCode() ?? 0;
            }

            // In-place insertion sort on the small buffer
            for (int i = 1; i < count; i++)
            {
                int key = buffer[i];
                int j = i - 1;

                // Move elements greater than 'key' one position ahead 
                while (j >= 0 && buffer[j] > key)
                {
                    buffer[j + 1] = buffer[j];
                    j--;
                }

                buffer[j + 1] = key;
            }

            // Now the span is sorted; group duplicates and combine hashes
            int baseHash = 0;
            int total = 0;

            int idx = 0;
            while (idx < count)
            {
                int currentHash = buffer[idx];
                int freq = 1;
                idx++;

                // Count how many duplicates of currentHash
                while (idx < count && buffer[idx] == currentHash)
                {
                    freq++;
                    idx++;
                }

                baseHash = HashCode.Combine(baseHash, currentHash, freq);
                total += freq;
            }

            // Final combine with total to avoid collisions from same frequency patterns
            return HashCode.Combine(baseHash, total);
        }

        public static int SetHash<T, V>(this IEnumerable<T> elements, Func<T, V> selector)
        {
            const int MaxSize = 8;

            // Stack allocation: no heap allocations here
            Span<int> buffer = stackalloc int[MaxSize];
            int count = 0;

            // Read hash codes into the buffer
            foreach (var element in elements)
            {
                if (count == MaxSize)
                    throw new InvalidOperationException("Too many elements in the set.");

                buffer[count++] = selector(element)?.GetHashCode() ?? 0;
            }

            // In-place insertion sort on the small buffer
            for (int i = 1; i < count; i++)
            {
                int key = buffer[i];
                int j = i - 1;

                // Move elements greater than 'key' one position ahead 
                while (j >= 0 && buffer[j] > key)
                {
                    buffer[j + 1] = buffer[j];
                    j--;
                }

                buffer[j + 1] = key;
            }

            // Now the span is sorted; group duplicates and combine hashes
            int baseHash = 0;
            int total = 0;

            int idx = 0;
            while (idx < count)
            {
                int currentHash = buffer[idx];
                int freq = 1;
                idx++;

                // Count how many duplicates of currentHash
                while (idx < count && buffer[idx] == currentHash)
                {
                    freq++;
                    idx++;
                }

                baseHash = HashCode.Combine(baseHash, currentHash, freq);
                total += freq;
            }

            // Final combine with total to avoid collisions from same frequency patterns
            return HashCode.Combine(baseHash, total);
        }

        public static int SetHash<T, V>(this List<T> elements, Func<T, V> selector)
        {
            const int MaxSize = 8;

            // Stack allocation: no heap allocations here
            Span<int> buffer = stackalloc int[MaxSize];
            int count = 0;

            // Read hash codes into the buffer
            foreach (var element in elements)
            {
                if (count == MaxSize)
                    throw new InvalidOperationException("Too many elements in the set.");

                buffer[count++] = selector(element)?.GetHashCode() ?? 0;
            }

            // In-place insertion sort on the small buffer
            for (int i = 1; i < count; i++)
            {
                int key = buffer[i];
                int j = i - 1;

                // Move elements greater than 'key' one position ahead 
                while (j >= 0 && buffer[j] > key)
                {
                    buffer[j + 1] = buffer[j];
                    j--;
                }

                buffer[j + 1] = key;
            }

            // Now the span is sorted; group duplicates and combine hashes
            int baseHash = 0;
            int total = 0;

            int idx = 0;
            while (idx < count)
            {
                int currentHash = buffer[idx];
                int freq = 1;
                idx++;

                // Count how many duplicates of currentHash
                while (idx < count && buffer[idx] == currentHash)
                {
                    freq++;
                    idx++;
                }

                baseHash = HashCode.Combine(baseHash, currentHash, freq);
                total += freq;
            }

            // Final combine with total to avoid collisions from same frequency patterns
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

        public static IEnumerable<KeyValuePair<KO, V>> MapKeys<KI, V, KO>(this IEnumerable<KeyValuePair<KI, V>> e, Func<KI, KO> map) =>
            e.Select(kvp => KeyValuePair.Create(map(kvp.Key), kvp.Value));
    }
}
