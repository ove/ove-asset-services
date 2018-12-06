using OVE.Service.NetworkTiles.QuadTree.Domain.Graph;
using OVE.Service.NetworkTiles.QuadTree.Tree;

namespace OVE.Service.NetworkTiles.QuadTree.Domain {
    public static class HeightAnnotator {
        public static void Annotate(QuadTreeNode<GraphObject> quad, int depth = 0) {
            // todo make a non recursive version ;)
            depth++;
            quad.Depth = depth;
            if (!quad.IsLeaf()) {
                foreach (var subQuad in quad.SubQuads) {
                    Annotate(subQuad, depth);
                }
            }
        }
    }
}