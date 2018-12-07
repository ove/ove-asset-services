using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OVE.Service.AssetManager.Domain;
using OVE.Service.Core.Assets;
using OVE.Service.Core.Extensions;
using OVE.Service.NetworkTiles.Domain;
using OVE.Service.NetworkTiles.Models;

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

        public NetworkTilesController(ILogger<NetworkTilesController> logger, IConfiguration configuration, QuadTreeRepository quadTreeRepository) {
            _logger = logger;
            _configuration = configuration;
            _quadTreeRepository = quadTreeRepository;
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

            var assetModel = await GetAssetById(id);

            var root = _quadTreeRepository.Request(assetModel);

            return root.ReturnMatchingLeaves(x, y, xWidth, yWidth)
                .Select(graphNode => assetModel.Project +"/"+ Path.GetFileNameWithoutExtension(assetModel.StorageLocation) + "/" + graphNode.Guid + ".json").ToList();// todo fix file names returned
        }

        /// <summary>
        /// return a list of the files within the archive.
        /// </summary>
        /// <param name="id">the asset id</param>
        /// <returns>a json list of the asset files</returns>
        [HttpGet]
        [Route("/api/NetworkTilesController/Details/{id}.{format?}")]
        public async Task<ActionResult<string>> GetArchiveContents(string id) {
            // todo see if this method is needed
            if (id == null) {
                return NotFound();
            }

            if (await GetAssetStatus(id) != NetworkTilesProcessingStates.Processed) {
                return NoContent();
            }

            var assetModel = await GetAssetById(id);

            if (assetModel == null) {
                return NotFound();
            }

            return this.FormatOrView(assetModel.AssetMeta);
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

            var assetModel = await GetAssetById(id);

            if (assetModel == null) {
                return NotFound();
            }

            var url = await GetAssetUri(assetModel);

            List<string> files = new List<string>();
            if (!string.IsNullOrWhiteSpace(assetModel.AssetMeta)) {
                try {
                    files  = JsonConvert.DeserializeObject<List<string>>(assetModel.AssetMeta);                    
                }
                catch (Exception ex) {
                    _logger.LogError(ex,"Bad meta data for asset "+id);
                }
            }
                

            var avm = new NetworkTilesViewModel {
                Id = assetModel.Id,
                AssetUrl = url,
                RootFile = assetModel.StorageLocation,
                Files = files
            };

            return View(avm);
        }

        private async Task<string> GetAssetUri(OVEAssetModel asset) {

            string url = _configuration.GetValue<string>("AssetManagerHostUrl").RemoveTrailingSlash() +
                         _configuration.GetValue<string>("AssetUrlApi") +
                         asset.Id;

            using (var client = new HttpClient()) {
                var responseMessage = await client.GetAsync(url);
                if (responseMessage.StatusCode == HttpStatusCode.OK) {
                    var assetString = await responseMessage.Content.ReadAsStringAsync();
                    _logger.LogInformation("About to download asset from url " + assetString);
                    return assetString;
                }
            }

            throw new Exception("Failed to get download URL for asset");
        }

        private async Task<NetworkTilesProcessingStates> GetAssetStatus(string id) {
            var res = await GetAssetById(id);

            string state = res.ProcessingState.ToString();
            return state != null && Enum.TryParse(state, out NetworkTilesProcessingStates assetState)
                ? assetState
                : NetworkTilesProcessingStates.Error;
        }

        private async Task<OVEAssetModel> GetAssetById(string id) {
            string url = _configuration.GetValue<string>("AssetManagerHostUrl").RemoveTrailingSlash() +
                         _configuration.GetValue<string>("GetAssetByIdApi") +
                         id + ".json";

            _logger.LogInformation("about to get on " + url);

            using (var client = new HttpClient()) {
                var responseMessage = await client.GetAsync(url);
                if (responseMessage.StatusCode == HttpStatusCode.OK) {
                    var assetString = await responseMessage.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(assetString)) {
                        return JsonConvert.DeserializeObject<OVEAssetModel>(assetString);
                    }
                }
            }

            return null;
        }
    }
}