using System;
using Microsoft.Extensions.Logging;
using OVE.Service.Core.Assets;
using OVE.Service.NetworkTiles.QuadTree.Domain.Graph;
using OVE.Service.NetworkTiles.QuadTree.Tree;
using OVE.Service.NetworkTiles.QuadTree.Utilities;

namespace OVE.Service.NetworkTiles.Domain {

    /// <summary>
    /// A Lazy concurrent dictionary to store loaded quad trees between requests
    /// </summary>
    public class QuadTreeRepository {
        private ILogger<QuadTreeRepository> _logger;

        private readonly LazyConcurrentDictionary<string, Tuple<OVEAssetModel, QuadTreeNode<GraphObject>>>
            _loadedQuadTrees = new LazyConcurrentDictionary<string, Tuple<OVEAssetModel, QuadTreeNode<GraphObject>>>();

        public QuadTreeRepository(ILogger<QuadTreeRepository> logger) {
            _logger = logger;
        }

        public QuadTreeNode<GraphObject> Request(OVEAssetModel asset) {
            return _loadedQuadTrees.GetOrAdd(asset.Id, id => LoadQuadTree(asset)).Item2;
        }

        private Tuple<OVEAssetModel, QuadTreeNode<GraphObject>> LoadQuadTree(OVEAssetModel asset) {
            QuadTreeNode<GraphObject> quadTree = null; //todo download quadtree and deserialize 
            return new Tuple<OVEAssetModel, QuadTreeNode<GraphObject>>(asset,quadTree);
        }

        public void Store(OVEAssetModel asset, QuadTreeNode<GraphObject> root) {
            _logger.LogInformation("Storing Quad for Asset "+asset.Id);
            _loadedQuadTrees.GetOrAdd(asset.Id, id => new Tuple<OVEAssetModel, QuadTreeNode<GraphObject>>(asset, root));
        }
    }
}