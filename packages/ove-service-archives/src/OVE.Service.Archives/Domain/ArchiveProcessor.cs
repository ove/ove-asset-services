using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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

namespace OVE.Service.Archives.Domain {
    // ReSharper disable once ClassNeverInstantiated.Global Dependency Injection
    public class ArchiveProcessor : IAssetProcessor<ArchiveProcessingStates> {
        private readonly ILogger _logger;
        private readonly IAssetFileOperations _fileOps;
        private readonly IConfiguration _configuration;

        public ArchiveProcessor(ILogger<ArchiveProcessor> logger,IAssetFileOperations fileOps,IConfiguration configuration) {
            _logger = logger;
            _fileOps = fileOps;
            _configuration = configuration;
        }

        #region Implementation of IAssetProcessor<ArchiveProcessingStates>

        public async Task Process(IAssetProcessingService<ArchiveProcessingStates> service, OVEAssetModel asset) {
            // 2) download it
            string url = await service.GetAssetUri(asset);

            string localUri = service.DownloadAsset(url,asset);

            // 3) unzip and Upload 
            await service.UpdateStatus(asset, (int) ArchiveProcessingStates.Uploading);

            List<string> files;
            using (var s = File.OpenRead(localUri)) {
                files = await UnZipAsset(asset, s);
            }

            // 4) set the meta data properly

            asset.AssetMeta = JsonConvert.SerializeObject(files);
            await UpdateAssetMeta(asset);

            // 5) delete local files 
            _logger.LogInformation("about to delete files");
            Directory.Delete(Path.GetDirectoryName(localUri), true);

            // 6) Mark it as completed            
            await service.UpdateStatus(asset, (int) ArchiveProcessingStates.Processed);
        }

        #endregion

        private async Task<List<string>> UnZipAsset(OVEAssetModel asset,Stream zipFile) {
            string prefixFolder = asset.StorageLocation.Split("/").FirstOrDefault() + "/";

            List<string> filesUploaded = new List<string>();

                var archive = new ZipArchive(zipFile);
                foreach (var entry in archive.Entries.Where(f => f.FullName.Contains("."))) {
                    // only files
                    var location = UnzipLocation(entry.FullName, prefixFolder);

                    using (var file = entry.Open()) {
                        //raw unzipped streams do not have length so copy to memory stream
                        using (var ms = new MemoryStream()) {
                            await file.CopyToAsync(ms);
                            await _fileOps.Upload(asset.Project, location, ms);
                        }
                    }

                    filesUploaded.Add(location);
                }

            return filesUploaded;
        }

        private string UnzipLocation(string entry, string prefixFolder) {
            var originalExt = Path.GetExtension(entry);
            var ext = _fileOps.SanitizeExtension(originalExt.ToLower());
            var location = prefixFolder + _fileOps.SanitizeFilename(entry.Replace(originalExt, ""), ext) + ext;
            return location;
        }

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
