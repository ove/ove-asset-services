using OVE.Service.Core.Assets;
using OVE.Service.NetworkTiles.QuadTree.Domain.Graph;
using OVE.Service.NetworkTiles.QuadTree.Tree;

namespace OVE.Service.NetworkTiles.Domain {
    public class CachedQuadTree {
        public CachedQuadTree(OVEAssetModel asset, QuadTreeNode<GraphObject> quadTree, string baseUrl) {
            Asset = asset;
            QuadTree = quadTree;
            BaseUrl = baseUrl;
        }

        public OVEAssetModel Asset { get; set; }
        public QuadTreeNode<GraphObject> QuadTree { get; set; }
        public string BaseUrl {get; set; }
        public int TotalBags { get; set; }
    }
}