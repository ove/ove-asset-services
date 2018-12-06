using OVE.Service.NetworkTiles.QuadTree.Domain.Graph;
using OVE.Service.NetworkTiles.QuadTree.Tree;
using OVE.Service.NetworkTiles.QuadTree.Utilities;

namespace OVE.Service.NetworkTiles.Domain {
    /// <summary>
    /// A Lazy concurrent dictionary to store loaded quad trees between requests
    /// </summary>
    public sealed class QuadTreeSingleton {
        // todo this might be better following the model of the Service Repository in AssetManager
        private static QuadTreeSingleton _instance;
        private static readonly object Padlock = new object();

        public readonly LazyConcurrentDictionary<string,QuadTreeNode<GraphObject>> LoadedQuadTrees = new LazyConcurrentDictionary<string, QuadTreeNode<GraphObject>>();
       
        private QuadTreeSingleton() {
        }

        public static QuadTreeSingleton Instance
        {
            get
            {
                lock (Padlock) {
                    return _instance ?? (_instance = new QuadTreeSingleton());
                }
            }
        }

    }
}