using OVE.Service.NetworkTiles.QuadTree.Tree;

namespace OVE.Service.NetworkTiles.QuadTree.Domain.Graph {
    public abstract class GraphObject : IQuadable<double> {
        /// <summary>
        /// Checks if this graph object is contained in the region
        /// represented by the quadtree node.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="q"></param>
        /// <returns></returns>
        public abstract bool IsWithin<T>(QuadTreeNode<T> q) where T : IQuadable<double>;
    }
}