using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OVE.Service.Archives.Domain;
using OVE.Service.Archives.Models;
using OVE.Service.AssetManager.Domain;
using OVE.Service.Core.Assets;
using OVE.Service.Core.Extensions;

namespace OVE.Service.Archives.Controllers {
    /// <summary>
    /// An API for the Archives service
    /// </summary>
    [ApiController]
    [FormatFilter]
    public class ArchiveController : Controller {
        private readonly ILogger<ArchiveController> _logger;
        private readonly IConfiguration _configuration;

        public ArchiveController(ILogger<ArchiveController> logger, IConfiguration configuration) {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// return a list of the files within the archive.
        /// </summary>
        /// <param name="id">the asset id</param>
        /// <returns>a json list of the asset files</returns>
        [HttpGet]
        [Route("/api/ArchiveController/Details/{id}.{format?}")]
        public async Task<ActionResult<string>> GetArchiveContents(string id) {
            if (id == null) {
                return NotFound();
            }

            if (await GetAssetStatus(id) != ArchiveProcessingStates.Processed) {
                return NoContent();
            }

            var assetModel = await GetAssetById(id);

            if (assetModel == null) {
                return NotFound();
            }

            return this.FormatOrView(assetModel.AssetMeta);
        }

        /// <summary>
        /// return a view of what is within the archive 
        /// </summary>
        /// <param name="id">id of the asset</param>
        /// <returns>view of the archive</returns>
        [HttpGet]
        [Route("/api/ArchiveController/ArchiveDetails/{id}.{format?}")]
        public async Task<ActionResult> ArchiveDetails(string id) {
            if (id == null) {
                return NotFound();
            }

            if (await GetAssetStatus(id) != ArchiveProcessingStates.Processed) {
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
                

            var avm = new ArchiveViewModel {
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

        private async Task<ArchiveProcessingStates> GetAssetStatus(string id) {
            var res = await GetAssetById(id);

            string state = res.ProcessingState.ToString();
            return state != null && Enum.TryParse(state, out ArchiveProcessingStates assetState)
                ? assetState
                : ArchiveProcessingStates.Error;
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