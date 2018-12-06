using System;
using System.Collections.Concurrent;
using System.Threading;

namespace OVE.Service.NetworkTiles.QuadTree.Utilities {
    /// <summary>
    /// a lazy loading dictionary which enables memory to be reclaimed if not used for some time
    /// via https://blogs.endjin.com/2015/10/using-lazy-and-concurrentdictionary-to-ensure-a-thread-safe-run-once-lazy-loaded-collection/
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class LazyConcurrentDictionary<TKey, TValue> {
        private readonly ConcurrentDictionary<TKey, Lazy<TValue>> _concurrentDictionary;

        public LazyConcurrentDictionary() {
            this._concurrentDictionary = new ConcurrentDictionary<TKey, Lazy<TValue>>();
        }

        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory) {
            var lazyResult = this._concurrentDictionary.GetOrAdd(key,
                k => new Lazy<TValue>(() => valueFactory(k), LazyThreadSafetyMode.ExecutionAndPublication));

            return lazyResult.Value;
        }
    }
}
