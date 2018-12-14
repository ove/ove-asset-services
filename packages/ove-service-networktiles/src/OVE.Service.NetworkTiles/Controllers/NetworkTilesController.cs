using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OVE.Service.AssetManager.Domain;
using OVE.Service.Core.Assets;
using OVE.Service.NetworkTiles.Domain;
using OVE.Service.NetworkTiles.Models;
using OVE.Service.NetworkTiles.QuadTree.Domain.Graph;
using OVE.Service.NetworkTiles.QuadTree.Tree;

namespace OVE.Service.NetworkTiles.Controllers {
    /// <summary>
    /// An API for the Network Tile Service
    /// </summary>
    [ApiController]
    [FormatFilter]
    public class NetworkTilesController : Controller {
        private readonly ILogger<NetworkTilesController> _logger;
        private readonly IConfiguration _configuration;
        private readonly QuadTreeRepository _quadTreeRepository;
        private readonly AssetApi _assetApi;

        public NetworkTilesController(ILogger<NetworkTilesController> logger, IConfiguration configuration, QuadTreeRepository quadTreeRepository, AssetApi assetApi) {
            _logger = logger;
            _configuration = configuration;
            _quadTreeRepository = quadTreeRepository;
            _assetApi = assetApi;
        }

        /// <summary>
        /// return a list of the files within the archive.
        /// </summary>
        /// <param name="id">the asset id</param>
        /// <returns>a json list of the asset files</returns>
        [HttpGet]
        [Route("/api/NetworkTilesController/GetFilesWithin/{id}/.{format?}")]
        public async Task<ActionResult<IEnumerable<string>>> GetFilesWithin(string id, double x, double y, double xWidth, double yWidth) {
            //todo this method is just sketched for testing
            if (id == null) {
                return NotFound();
            }

            if (await GetAssetStatus(id) != NetworkTilesProcessingStates.Processed) {
                return NoContent();
            }

            var assetModel = await _assetApi.GetAssetById(id);

            var cachedQuad = _quadTreeRepository.Request(assetModel);

            List<QuadTreeNode<GraphObject>> leaves = cachedQuad.QuadTree.ReturnMatchingLeaves(x, y, xWidth, yWidth);

            return leaves
                .Select(graphNode => assetModel.Project +"/"+ Path.GetFileNameWithoutExtension(assetModel.StorageLocation) + "/" + graphNode.Guid + ".json").ToList();// todo fix file names returned
        }

        /// <summary>
        /// LoadNetwork into memory 
        /// </summary> 
        /// <param name="id">the asset id</param>
        /// <param name="clients">number of expected rendering clients</param>
        /// <param name="bagsPerClient">number of bags each client can render</param>
        /// <returns>boundaries of the graph</returns>
        [HttpGet]
        [Route("/api/NetworkTilesController/LoadNetwork/{id}.{format?}")]
        public async Task<ActionResult<string>> LoadNetwork(string id, int clients,int bagsPerClient) {
            
            if (id == null) {
                return NotFound();
            }

            if (await GetAssetStatus(id) != NetworkTilesProcessingStates.Processed) {
                return NoContent();
            }

            var assetModel = await _assetApi.GetAssetById(id);

            if (assetModel == null) {
                return NotFound();
            }

            var cache = _quadTreeRepository.Request(assetModel);
            if (cache == null) {
                return NoContent();
            }

            cache.Clients = clients;
            cache.BagsPerClient = bagsPerClient;

            return JsonConvert.SerializeObject(cache.QuadTree.Centroid);
        }

        /// <summary>
        /// return a view of what is within the network tile set 
        /// </summary>
        /// <param name="id">id of the asset</param>
        /// <returns>view of the network tile set</returns>
        [HttpGet]
        [Route("/api/NetworkTilesController/NetworkTilesDetails/{id}.{format?}")]
        public async Task<ActionResult> NetworkTilesDetails(string id) {
            if (id == null) {
                return NotFound();
            }

            if (await GetAssetStatus(id) != NetworkTilesProcessingStates.Processed) {
                return NoContent();
            }

            var assetModel = await _assetApi.GetAssetById(id);

            if (assetModel == null) {
                return NotFound();
            }

            var url = await _assetApi.GetAssetUri(assetModel);
            string baseUrl = url.Replace(Path.GetExtension(url), "/");
            var quadUrl = baseUrl + "quad.json";
            
            var avm = new NetworkTilesViewModel {
                Id = assetModel.Id,
                AssetUrl = url,
                RootFile = quadUrl,
            };

            return View(avm);
        }

        private async Task<NetworkTilesProcessingStates> GetAssetStatus(string id) {
            var res = await _assetApi.GetAssetById(id);

            string state = res.ProcessingState.ToString();
            return state != null && Enum.TryParse(state, out NetworkTilesProcessingStates assetState)
                ? assetState
                : NetworkTilesProcessingStates.Error;
        }

        
    }
}