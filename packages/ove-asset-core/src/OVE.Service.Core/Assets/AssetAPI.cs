using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OVE.Service.Core.Extensions;

namespace OVE.Service.Core.Assets {
    /// <summary>
    /// A set of helper methods for interacting with Assets via the Asset Manager API 
    /// </summary>
    public class AssetApi {
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;

        public AssetApi(IConfiguration configuration, ILogger logger) {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<string> GetAssetUri(OVEAssetModel asset) {

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

        public async Task<OVEAssetModel> GetAssetById(string id) {
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
