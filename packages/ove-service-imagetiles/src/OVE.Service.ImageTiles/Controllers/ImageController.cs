using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using OVE.Service.ImageTiles.Domain;
using OVE.Service.ImageTiles.Models;

namespace OVE.Service.ImageTiles.Controllers {
    /// <summary>
    /// An API for the Image Tile service
    /// API required:
    /// 1) get .dzi file for asset ID
    /// 2) viewer HTML
    /// </summary>
    [ApiController]
    [FormatFilter]
    public class ImageController : Controller {
        private readonly ILogger<ImageController> _logger;
        private readonly IConfiguration _configuration;

        public ImageController(ILogger<ImageController> logger, IConfiguration configuration) {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// return a HTML view of the image
        /// </summary>
        /// <param name="id">id of the asset</param>
        /// <returns></returns>
        [HttpGet]
        [Route("/api/ImageController/ViewImage/")]
        public async Task<IActionResult> ViewImage(string id) {
            return View(new ImageViewModel {
                Id = id, 
                AssetUrl = await GetAssetUrl(id), 
                DziUrl = (await GetDZIbyId(id))?.Value
            });
        }

        /// <summary>
        /// Return details of the service registration
        /// </summary>
        /// <param name="project">project the asset belongs to</param>
        /// <param name="name">name of the asset</param>
        /// <returns>url of dzi file or No Content if not processed yet</returns>
        [HttpGet]
        [Route("/api/ImageController/GetDZIFileByProjectName/")]
        public async Task<ActionResult<string>> GetDZIFileByProjectName(string project, string name) {

            string id = await FindAssetById(project, name);
            if (string.IsNullOrWhiteSpace(id)) {
                return NotFound();
            }

            return await GetDZIbyId(id);
        }

        private async Task<string> FindAssetById(string project, string name) {
            string url = _configuration.GetValue<string>("AssetManagerHostUrl").RemoveTrailingSlash() +
                         _configuration.GetValue<string>("GetAssetByProjectName") +
                         "?project=" + project + "&name=" + name;

            _logger.LogInformation("about to get on " + url);

            using (var client = new HttpClient()) {
                var responseMessage = await client.GetAsync(url);
                if (responseMessage.StatusCode == HttpStatusCode.OK) {
                    var assetString = await responseMessage.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(assetString)) {
                        return assetString.Replace("\"", "");
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Return the URL of the dzi file if it has been processed 
        /// </summary>
        /// <param name="id">id of the asset</param>
        /// <returns>url of dzi file or No Content if not processed yet</returns>
        [HttpGet]
        [Route("/api/ImageController/GetDZIFile/{id}")]
        public async Task<ActionResult<string>> GetDZIbyId(string id) {

            if ((await GetAssetStatus(id)) != ProcessingStates.Processed) {
                return NoContent();
            }

            string url = await GetAssetUrl(id);
            if (string.IsNullOrWhiteSpace(url)) {
                return NotFound();
            }

            var dzi = url.Substring(0, url.LastIndexOf('.'));
            dzi += ".dzi";
            return dzi;
        }

        private async Task<ProcessingStates> GetAssetStatus(string id) {
            string url = _configuration.GetValue<string>("AssetManagerHostUrl").RemoveTrailingSlash() +
                         _configuration.GetValue<string>("GetAssetByIdApi") +
                         id + ".json";

            _logger.LogInformation("about to get on " + url);

            using (var client = new HttpClient()) {
                var responseMessage = await client.GetAsync(url);
                if (responseMessage.StatusCode == HttpStatusCode.OK) {
                    var assetString = await responseMessage.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(assetString)) {
                        var asset = JObject.Parse(assetString);
                        string state = asset["processingState"].ToString();
                        if (Enum.TryParse(state, out ProcessingStates assetState)) {
                            return assetState;
                        }
                    }
                }
            }

            return ProcessingStates.Error;
        }

        private async Task<string> GetAssetUrl(string id) {
            string url = _configuration.GetValue<string>("AssetManagerHostUrl").RemoveTrailingSlash() +
                         _configuration.GetValue<string>("AssetUrlApi") +
                         id;

            _logger.LogInformation("about to get on " + url);

            using (var client = new HttpClient()) {
                var responseMessage = await client.GetAsync(url);
                if (responseMessage.StatusCode == HttpStatusCode.OK) {
                    var assetString = await responseMessage.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(assetString)) {
                        return assetString;
                    }
                }
            }

            return null;
        }
    }
}
