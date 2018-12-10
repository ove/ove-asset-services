using System.Collections.Generic;
using System.Linq;

namespace OVE.Service.NetworkTiles.QuadTree.Utilities {
    public static class BatchLinq {
        /// <summary>
        /// this groups the read objects into groups of objects 
        /// </summary>
        /// <param name="graphObj">The graph objects </param>
        /// <param name="batchSize"></param>
        /// <returns></returns>
        public static IEnumerable<List<TK>> BatchObjects<T, TK>(IEnumerable<T> graphObj, int batchSize = 500) where T : TK {
            var enumerator = graphObj.GetEnumerator();
            List<TK> list = new List<TK>();
            int count = 0;
            while (enumerator.MoveNext()) {

                list.Add(enumerator.Current);
                count++;
                if (count >= batchSize) {
                    yield return list;
                    list = new List<TK>();
                    count = 0;
                }
            }

            if (list.Any()) {
                yield return list;
            }

            enumerator.Dispose();
        }
    }
}