using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
        private readonly QuadTreeRepository _quadTreeRepository;
        private readonly AssetApi _assetApi;

        public NetworkTilesController(ILogger<NetworkTilesController> logger, QuadTreeRepository quadTreeRepository, AssetApi assetApi) {
            _logger = logger;
            _quadTreeRepository = quadTreeRepository;
            _assetApi = assetApi;
        }

        /// <summary>
        /// return a list of the files within the archive.
        /// </summary>
        /// <param name="id">the asset id</param>
        /// <param name="x">centroid x</param>
        /// <param name="y">centroid y</param>
        /// <param name="xWidth">width of box in x</param>
        /// <param name="yWidth">width of box in y</param>
        /// <returns>a json list of the asset files</returns>
        [HttpGet]
        [Route("/api/NetworkTilesController/GetFilesWithin/{id}/.{format?}")]
        public ActionResult<string> GetFilesWithin(string id, double x, double y, double xWidth, double yWidth) {
            if (id == null) {
                return NotFound();
            }

            var cachedQuad = _quadTreeRepository.Request(id);
            if (cachedQuad == null) {
                return NoContent();
            }

            List<QuadTreeNode<GraphObject>> leaves = cachedQuad.QuadTree.ReturnMatchingLeaves(x, y, xWidth, yWidth);
            _logger.LogInformation($"Found {leaves.Count} Leaves in a  search");
            return ReturnLeaves(leaves, cachedQuad);
        }

        private static ActionResult<string> ReturnLeaves(List<QuadTreeNode<GraphObject>> leaves, CachedQuadTree cachedQuad) {
            var res = leaves.Select(l => new {
                Id = l.Guid,
                Centriod = l.Centroid,
                Url = cachedQuad.BaseUrl + l.Guid + ".json"
            }).ToArray();

            return JsonConvert.SerializeObject(res);
        }

        /// <summary>
        /// return a list of the files within the archive.
        /// </summary>
        /// <param name="id">the asset id</param>
        /// <param name="x">centroid x</param>
        /// <param name="y">centroid y</param>
        /// <param name="xWidth">width of box in x</param>
        /// <param name="yWidth">width of box in y</param>
        /// <returns>a json list of the asset files</returns>
        [HttpGet]
        [Route("/api/NetworkTilesController/GetSparseFilesWithin/{id}/.{format?}")]
        public ActionResult<string> GetSparseFilesWithin(string id, double x, double y, double xWidth, double yWidth) {
            if (id == null) {
                return NotFound();
            }

            var cachedQuad = _quadTreeRepository.Request(id);
            if (cachedQuad == null) {
                return NoContent();
            }

            List<QuadTreeNode<GraphObject>> leaves = cachedQuad.QuadTree.ReturnSparseMatchingLeaves(x, y, xWidth, yWidth,cachedQuad.TotalBags);
            _logger.LogInformation($"Found {leaves.Count} Leaves in a Sparse search");
            return ReturnLeaves(leaves, cachedQuad);
        }

        /// <summary>
        /// LoadNetwork into memory 
        /// </summary> 
        /// <param name="id">the asset id</param>
        /// <param name="totalBags">number of bags which may be rendered</param>
        /// <returns>boundaries of the graph</returns>
        [HttpGet]
        [Route("/api/NetworkTilesController/LoadNetwork/{id}.{format?}")]
        public async Task<ActionResult<string>> LoadNetwork(string id, int totalBags) {
            _logger.LogInformation("about to load quad for "+id);
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

            var cache = _quadTreeRepository.Load(assetModel);
            if (cache == null) {
                return NoContent();
            }

            cache.TotalBags = totalBags;

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
            _logger.LogInformation("finding view for  "+id);
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