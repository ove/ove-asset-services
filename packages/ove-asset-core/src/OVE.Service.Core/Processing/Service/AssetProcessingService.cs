using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OVE.Service.Core.Assets;
using OVE.Service.Core.Extensions;

namespace OVE.Service.Core.Processing.Service {
    /// <summary>
    /// This holds the microservice logic for processing assets.
    /// The generic parameter holds the domain specific logic. 
    /// </summary>
    /// <typeparam name="TP">the domain specific IAssetProcessor</typeparam>
    /// <typeparam name="TV">the domain specific Enum type for states</typeparam>
    // ReSharper disable once ClassNeverInstantiated.Global Dependency Injection ;)
    public class AssetProcessingService<TP,TV> : IHostedService, IDisposable, IAssetProcessingService<TV> 
        where TP : IAssetProcessor<TV> 
        where TV :  struct, IConvertible {// todo when .net 7.3 reaches .net core this type constraint should be System.Enum then the casts to int can disappear. 
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly TP _processor;
        private Timer _timer;
        private const string LocalStorageBasePath = "LocalStorage:BasePath";
        private const string ProcessingMaxConcurrent = "AssetProcessing:MaxConcurrent";
        private const string ProcessingPollSeconds = "AssetProcessing:PollSeconds";

        private const int ProcessingErrorState = -1;

        private readonly SemaphoreSlim _processing;
        private readonly int _maxConcurrent;

        public AssetProcessingService(ILogger<AssetProcessingService<TP,TV>> logger, IConfiguration configuration,
            TP processor) {
            _logger = logger;
            _configuration = configuration;
            _processor = processor;

            _maxConcurrent = _configuration.GetValue<int>(ProcessingMaxConcurrent);
            _processing = new SemaphoreSlim(_maxConcurrent, _maxConcurrent);
        }

        public Task StartAsync(CancellationToken cancellationToken) {
            _logger.LogInformation("Async Asset Processor Service is starting.");
            _timer = new Timer(ProcessAsset, null, TimeSpan.Zero,
                TimeSpan.FromSeconds(_configuration.GetValue<int>(ProcessingPollSeconds)));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) {
            _logger.LogWarning("Async Asset Processor Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose() {
            _timer?.Dispose();
        }

        /// <summary>
        /// This method calls the asset manger seek an asset to process
        /// </summary>
        /// <param name="state"></param>
        private async void ProcessAsset(object state) {
            if (!_processing.Wait(10)) {
                _logger.LogInformation("Tried to fire an asset processor but too many threads already running");
                return;
            }
            _logger.LogInformation("About to seek an Asset to process");

            OVEAssetModel asset = null;
            try {
                // 1) get an Asset to process
                asset = await FindAssetToProcess();

                if (asset == null) {
                    _logger.LogInformation("no work for an asset Processor, running Processors = " +
                                           (_maxConcurrent - _processing.CurrentCount - 1));
                } else {
                    _logger.LogInformation("Found asset " + asset.Id);

                    await _processor.Process(this, asset);
                }
            } catch (Exception e) {

                _logger.LogError(e, "Exception in Asset Processing");

                try {

                    if (asset != null) {
                        await UpdateStatus(asset, ProcessingErrorState, e.ToString());
                    }
                } catch (Exception ex) {
                    _logger.LogError(ex, "Exception in Asset Processing");
                }
            } finally {
                _processing.Release();
                _logger.LogInformation("released processing lock");
            }

        }

        public async Task<bool> UpdateStatus(OVEAssetModel asset, int state, string errors = null) {// todo int should be TV, see type constraint 

            var url = _configuration.GetValue<string>("AssetManagerHostUrl").RemoveTrailingSlash() +
                      _configuration.GetValue<string>("SetStateApi") +
                      asset.Id + "/" + state;

            if (errors != null) {
                url += "?message=" + Uri.EscapeDataString(errors);
            }

            _logger.LogInformation("Setting Asset State to " + state); 
            using (var client = new HttpClient()) {
                var responseMessage = await client.PostAsync(url, new StringContent(""));
                if (responseMessage.StatusCode != HttpStatusCode.OK) {
                    _logger.LogError("Failed to set asset status " + responseMessage.StatusCode);
                    return false;
                }
            }

            return true;
        }

        public string GetLocalAssetBasePath() {
            var rootDirectory = _configuration.GetValue<string>(WebHostDefaults.ContentRootKey);
            var filepath = Path.Combine(rootDirectory, _configuration.GetValue<string>(LocalStorageBasePath));
            if (!Directory.Exists(filepath)) {
                _logger.LogInformation("Creating directory for Assets " + filepath);
                Directory.CreateDirectory(filepath);
            }

            return filepath;
        }

        public string DownloadAsset(string url, OVEAssetModel asset) {
            // make temp directory
            // download url

            string localFile = Path.Combine(GetLocalAssetBasePath(), asset.StorageLocation);
            Directory.CreateDirectory(Path.GetDirectoryName(localFile));

            _logger.LogInformation("About to download to " + localFile);

            using (var client = new WebClient()) {
                client.DownloadFile(new Uri(url), localFile);
            }

            _logger.LogInformation("Finished downloading to " + localFile);

            return localFile.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
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

        private async Task<OVEAssetModel> FindAssetToProcess() {
            OVEAssetModel todo = null;
            string url = _configuration.GetValue<string>("AssetManagerHostUrl").RemoveTrailingSlash() +
                         _configuration.GetValue<string>("WorkItemApi") +
                         _configuration.GetValue<string>("ServiceName") + ".json";

            _logger.LogInformation("about to get work item from url " + url);

            using (var client = new HttpClient()) {
                var responseMessage = await client.GetAsync(url);

                _logger.LogInformation("Response was " + responseMessage.StatusCode);
                if (responseMessage.StatusCode == HttpStatusCode.OK) {
                    var assetString = await responseMessage.Content.ReadAsStringAsync();
                    todo = JsonConvert.DeserializeObject<OVEAssetModel>(assetString);
                }
            }

            return todo;
        }
    }
}