using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OVE.Service.Core.Assets;
using OVE.Service.Core.Extensions;
using OVE.Service.Core.FileOperations;
using OVE.Service.Core.Processing;
using OVE.Service.NetworkTiles.QuadTree;
using OVE.Service.NetworkTiles.QuadTree.Domain.Graph;
using OVE.Service.NetworkTiles.QuadTree.Tree;

namespace OVE.Service.NetworkTiles.Domain {
    // ReSharper disable once ClassNeverInstantiated.Global Dependency Injection
    public class NetworkTilesProcessor : IAssetProcessor<NetworkTilesProcessingStates> {
        private readonly ILogger _logger;
        private readonly IAssetFileOperations _fileOps;
        private readonly IConfiguration _configuration;

        public NetworkTilesProcessor(ILogger<NetworkTilesProcessor> logger,IAssetFileOperations fileOps,IConfiguration configuration) {
            _logger = logger;
            _fileOps = fileOps;
            _configuration = configuration;
        }

        #region Implementation of IAssetProcessor<NetworkTilesProcessor>

        public async Task Process(IAssetProcessingService<NetworkTilesProcessingStates> service, OVEAssetModel asset) {
            // 2) download it
            string url = await service.GetAssetUri(asset);

            string localUri = service.DownloadAsset(url,asset);

            // 3) start processing
            await service.UpdateStatus(asset, (int) NetworkTilesProcessingStates.CreatingQuadTree);

            // do processing
            QuadTreeNode<GraphObject> root = QuadTreeProcessor.ProcessFile(localUri,_logger);
            // store it in memory
            QuadTreeSingleton.Instance.LoadedQuadTrees.GetOrAdd(asset.StorageLocation, id => root);

            // 4) upload the results 
            await service.UpdateStatus(asset, (int) NetworkTilesProcessingStates.Uploading);

            string folder = Path.GetDirectoryName(localUri) + Path.DirectorySeparatorChar +
                            Path.GetFileNameWithoutExtension(localUri);
            await _fileOps.UploadDirectory(folder,asset);

            // 5) set the meta data properly

            asset.AssetMeta = JsonConvert.SerializeObject("todo");// todo not sure what goes in here yet - some stats about the quadtree?
            await UpdateAssetMeta(asset);

            // 6) delete local files 
             _logger.LogInformation("about to delete files"); 
             Directory.Delete(Path.GetDirectoryName(localUri), true);

            // 7) Mark it as completed            
            await service.UpdateStatus(asset, (int) NetworkTilesProcessingStates.Processed);
        }

        #endregion

        private async Task<bool> UpdateAssetMeta(OVEAssetModel asset) {
            var url = _configuration.GetValue<string>("AssetManagerHostUrl").RemoveTrailingSlash() +
                      _configuration.GetValue<string>("UpdateMetaApi") + 
                      asset.Id+".json";

            _logger.LogInformation("Updating Asset Metadata"); 
            using (var client = new HttpClient()) {
                var body = new StringContent(asset.AssetMeta);
                body.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
                var responseMessage = await client.PostAsync(url,  body );
                if (responseMessage.StatusCode != HttpStatusCode.OK) {
                    _logger.LogError("Failed to update the asset metadata " + responseMessage.StatusCode);
                    return false;
                }
            }

            return true;
        }

    }
}
