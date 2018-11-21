using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OVE.Service.ImageTiles.Models;

namespace OVE.Service.ImageTiles.Domain {
    public class ASyncImageProcessor : IHostedService, IDisposable {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly ImageProcessor _processor;
        private Timer _timer;

        private readonly SemaphoreSlim _processing;
        private int maxConcurrent;

        public ASyncImageProcessor(ILogger<ASyncImageProcessor> logger, IConfiguration configuration, ImageProcessor processor) {
            _logger = logger;
            _configuration = configuration;
            _processor = processor;
            maxConcurrent = _configuration.GetValue<int>("ImageProcessingConfig:MaxConcurrent");
            _processing = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        }

        public Task StartAsync(CancellationToken cancellationToken) {
            _logger.LogInformation("Async Image Processor Service is starting.");

            _timer = new Timer(ProcessImage, null, TimeSpan.Zero,
                TimeSpan.FromSeconds(_configuration.GetValue<int>("ImageProcessingConfig:PollSeconds")));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) {
            _logger.LogInformation("Async Image Processor Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose() {
            _timer?.Dispose();
        }

        /// <summary>
        /// Steps to process an image:
        /// 1) Get the asset to process
        /// 2) Download it
        /// 3) Create DZI
        /// 4) Upload it
        /// 5) Mark it as completed
        /// 
        /// </summary>
        /// <param name="state"></param>
        private async void ProcessImage(object state) {
            if (!_processing.Wait(10)) {
                _logger.LogInformation("Tried to fire Image Processing but too many threads already running");
                return;
            }

            OVEAssetModel asset = null;
            try {
                // 1) get an Asset to process
                asset = await FindAssetToProcess();

                if (asset == null) {
                    _logger.LogInformation("no work for Image Processor, running Processors = " +
                                           (maxConcurrent - _processing.CurrentCount - 1));
                }
                else {
                    _logger.LogInformation("Found asset "+asset.Id);

                    // 2) download it
                    string url = await GetAssetUri(asset);

                    string localUri = DownloadAsset(url,asset);

                    // 3) Create DZI file 
                    await UpdateStatus(asset, ProcessingStates.CreatingDZI);
                    var res = _processor.ProcessFile(localUri);
                    _logger.LogInformation("Processed file "+res);

                    // 4) Upload it
                    await UpdateStatus(asset, ProcessingStates.Uploading);
                    await UploadDirectory(localUri,asset);
                    
                    // 5) delete local files 
                    _logger.LogInformation("about to delete files");
                    Directory.Delete(Path.GetDirectoryName(localUri), true);

                    // 6) Mark it as completed            
                    await UpdateStatus(asset, ProcessingStates.Processed);
                }
            } catch (Exception e) {
                
                _logger.LogError(e, "Exception in Image Processing");
                if (asset != null) {
                    await UpdateStatus(asset,ProcessingStates.Error,e.ToString());
                }
            } finally {
                _processing.Release();
            }

        }

        private async Task<bool> UpdateStatus(OVEAssetModel asset, ProcessingStates state, string errors = null) {
            
            var url = _configuration.GetValue<string>("AssetManagerHostUrl").RemoveTrailingSlash() +
                      _configuration.GetValue<string>("SetStateApi") +
                      asset.Id + "/" + (int) state;

            if (errors != null) {
                url += "?message=" + Uri.EscapeDataString(errors);
            }
            _logger.LogInformation("Setting Asset Status to "+state);
            using (var client = new HttpClient()) {
                var responseMessage = await client.PostAsync(url,new StringContent(""));
                if (responseMessage.StatusCode != HttpStatusCode.OK) {
                    _logger.LogError("Failed to set asset status "+responseMessage.StatusCode);
                    return false;
                }
            }

            return true;
        }

        private const string S3ClientAccessKey = "s3Client:AccessKey";
        private const string S3ClientSecret = "s3Client:Secret";
        private const string S3ClientServiceUrl = "s3Client:ServiceURL";

        private IAmazonS3 GetS3Client() {
            
            IAmazonS3 s3Client = new AmazonS3Client(
                _configuration.GetValue<string>(S3ClientAccessKey),
                _configuration.GetValue<string>(S3ClientSecret),
                new AmazonS3Config {
                    ServiceURL = _configuration.GetValue<string>(S3ClientServiceUrl).EnsureTrailingSlash(),
                    UseHttp = true, 
                    ForcePathStyle = true
                }
            );
            _logger.LogInformation("Created new S3 Client");
            return s3Client;
        }

        private async Task<bool> UploadDirectory(string file, OVEAssetModel asset) {
            _logger.LogInformation("about to upload directory " + file);

            using (var fileTransferUtility = new TransferUtility(GetS3Client())) {

                // upload the .dzi file
                var assetRootFolder = Path.GetDirectoryName(asset.StorageLocation);

                var fileDirectory = Path.ChangeExtension(file, ".dzi").Replace(".dzi", "_files");

                var filesKeyPrefix =
                    assetRootFolder + "/" + new DirectoryInfo(fileDirectory).Name + "/"; // upload to the right folder

                TransferUtilityUploadRequest req = new TransferUtilityUploadRequest() {
                    BucketName = asset.Project,
                    Key = Path.ChangeExtension(asset.StorageLocation, ".dzi"),
                    FilePath = Path.ChangeExtension(file, ".dzi")

                };
                await fileTransferUtility.UploadAsync(req);

                // upload the tile files 

                TransferUtilityUploadDirectoryRequest request =
                    new TransferUtilityUploadDirectoryRequest() {
                        KeyPrefix = filesKeyPrefix,
                        Directory = fileDirectory,
                        BucketName = asset.Project,
                        SearchOption = SearchOption.AllDirectories,
                        SearchPattern = "*.*"
                    };

                await fileTransferUtility.UploadDirectoryAsync(request);

                _logger.LogInformation("finished upload for "+file);

                return true;
            }
        }

        public string GetImagesBasePath() {
            var rootDirectory = _configuration.GetValue<string>(WebHostDefaults.ContentRootKey);
            var filepath = Path.Combine(rootDirectory, _configuration.GetValue<string>("ImageStorageConfig:BasePath"));
            if (!Directory.Exists(filepath)) {
                _logger.LogInformation("Creating directory for images " + filepath);
                Directory.CreateDirectory(filepath);
            }
            return filepath;
        }

        private string DownloadAsset(string url, OVEAssetModel asset) {
            // make temp directory
            // download url

            string localFile = Path.Combine(GetImagesBasePath(), asset.StorageLocation);
            Directory.CreateDirectory(Path.GetDirectoryName(localFile));

            _logger.LogInformation("About to download to " + localFile);

            using (var client = new WebClient()) {
                client.DownloadFile(new Uri(url), localFile);
            }

            _logger.LogInformation("Finished downloading to " + localFile);

            return localFile.Replace('/',Path.DirectorySeparatorChar).Replace('\\',Path.DirectorySeparatorChar);
        }

        private async Task<string> GetAssetUri(OVEAssetModel asset) {
            
        string url = _configuration.GetValue<string>("AssetManagerHostUrl").RemoveTrailingSlash() +
                         _configuration.GetValue<string>("AssetUrlApi") +
                         asset.Id;

            using (var client = new HttpClient()) {
                var responseMessage = await client.GetAsync(url);
                if (responseMessage.StatusCode == HttpStatusCode.OK) {
                    var assetString = await responseMessage.Content.ReadAsStringAsync();
                    _logger.LogInformation("About to download asset from url "+assetString);
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
