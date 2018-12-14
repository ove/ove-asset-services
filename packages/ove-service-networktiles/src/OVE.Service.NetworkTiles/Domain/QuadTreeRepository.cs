using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OVE.Service.Core.Assets;
using OVE.Service.NetworkTiles.QuadTree.Domain.Graph;
using OVE.Service.NetworkTiles.QuadTree.Tree;
using OVE.Service.NetworkTiles.QuadTree.Utilities;

namespace OVE.Service.NetworkTiles.Domain {
    /// <summary>
    /// A Lazy concurrent dictionary to store loaded quad trees between requests
    /// </summary>
    public class QuadTreeRepository {
        private readonly ILogger<QuadTreeRepository> _logger;
        private readonly AssetApi _assetApi;

        private readonly LazyConcurrentDictionary<string, CachedQuadTree>
            _loadedQuadTrees = new LazyConcurrentDictionary<string, CachedQuadTree>();

        public QuadTreeRepository(ILogger<QuadTreeRepository> logger, AssetApi assetApi) {
            _logger = logger;
            _assetApi = assetApi;
        }

        public CachedQuadTree Request(OVEAssetModel asset) {
            return _loadedQuadTrees.GetOrAdd(asset.Id, id => Task.Run(() => LoadQuadTreeAsync(asset)).Result);
            // note this is not async but i don't like the idea of an async concurrent dictionary which is the alternative 
        }

        private async Task<CachedQuadTree> LoadQuadTreeAsync(OVEAssetModel asset) {
            
            if ( (NetworkTilesProcessingStates)asset.ProcessingState != NetworkTilesProcessingStates.Processed) {
                _logger.LogWarning("Tried to access an unprocessed quad tree for asset "+asset.Id);
                return null;
            }

            string url = await _assetApi.GetAssetUri(asset); 
            string baseUrl = url.Replace(Path.GetExtension(url), "/");
            url = baseUrl + "quad.json";

            QuadTreeNode<GraphObject> quadTree = null;

            try {
                using (var wc = new WebClient()) {
                    string serialized = wc.DownloadString(url);
                    quadTree = JsonConvert.DeserializeObject<QuadTreeNode<GraphObject>>(serialized);
                }
            } catch (Exception e) {
                _logger.LogError(e,"failed to deserialize a quad tree for asset "+asset.Id);
            }

            return new CachedQuadTree(asset,quadTree,baseUrl);
        }

        public async void Store(OVEAssetModel asset, QuadTreeNode<GraphObject> root) {
            _logger.LogInformation("Storing Quad for Asset "+asset.Id);

            string url = await _assetApi.GetAssetUri(asset);
            string baseUrl = url.Replace(Path.GetExtension(url), "/");
            
            _loadedQuadTrees.GetOrAdd(asset.Id, id => new CachedQuadTree(asset, root,baseUrl));
        }
    }
}