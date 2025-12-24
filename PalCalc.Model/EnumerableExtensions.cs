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

        public static ParallelQuery<IEnumerable<T>> BatchedAsParallel<T>(this List<T> elements) =>
            elements.Batched(elements.PreferredParallelBatchSize()).AsParallel();

        public static IEnumerable<T> SkipNull<T>(this IEnumerable<T> elements) => elements.Where(v => v != null);

        public static V GetValueOrElse<K, V>(this IDictionary<K, V> dict, K key, V fallback)
        {
            if (dict.ContainsKey(key)) return dict[key];
            else return fallback;
        }

        public static V GetValueFromAny<K, V>(this IDictionary<K, V> dict, params K[] keys) => dict[keys.First(dict.ContainsKey)];

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

        #region SetHash

        // method for making an order-independent hash of a collection
        //
        // these are used in hotpaths in the Solver, and are implemented to minimize memory allocations

        // in practice this is only used to hash lists of passive skills. max of 4 passives across
        // 2 parents - generally just need to handle up to 8 items
        const int SetHashMaxSize = 8;

        public static int SetHash<T>(this IEnumerable<T> elements)
        {
            // Stack allocation: no heap allocations here
            Span<int> buffer = stackalloc int[SetHashMaxSize];
            int count = 0;

            // Read hash codes into the buffer
            foreach (var element in elements)
            {
                if (count == SetHashMaxSize)
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

        // (copy of above impl. with slightly different signature to avoid need for iterator in a Solver hotpath)
        public static int SetHash<T, V>(this IReadOnlyList<T> elements, Func<T, V> selector)
        {
            // Stack allocation: no heap allocations here
            Span<int> buffer = stackalloc int[SetHashMaxSize];
            int count = elements.Count;
            if (count > SetHashMaxSize)
                throw new InvalidOperationException("Too many elements in the set.");

            // Read hash codes into the buffer
            for (int i = 0; i < elements.Count; i++)
            {
                buffer[i] = selector(elements[i])?.GetHashCode() ?? 0;
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

        // (copy of above impl. with slightly different signature to avoid need for iterator in a Solver hotpath)
        public static int ListSetHash<T>(this IReadOnlyList<T> elements)
        {
            // Stack allocation: no heap allocations here
            Span<int> buffer = stackalloc int[SetHashMaxSize];
            int count = 0;

            // Read hash codes into the buffer
            foreach (var element in elements)
            {
                if (count == SetHashMaxSize)
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

        #endregion
    }
}
