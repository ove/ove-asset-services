using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetVips;
using OVE.Service.Core.Assets;
using OVE.Service.Core.FileOperations;
using OVE.Service.Core.Processing;

namespace OVE.Service.ImageTiles.Domain {
    public class ImageProcessor : IAssetProcessor<ImageProcessingStates> {
        private readonly ILogger<ImageProcessor> _logger;
        private readonly IAssetFileOperations _fileOps;

        public ImageProcessor(ILogger<ImageProcessor> logger,IAssetFileOperations fileOps) {
            _logger = logger;
            _fileOps = fileOps;
            VipsStartup();
        }

        #region Implementation of IAssetProcessor

        public async Task Process(IAssetProcessingService<ImageProcessingStates> service, OVEAssetModel asset) {
            // 2) download it
            string url = await service.GetAssetUri(asset);

            string localUri = service.DownloadAsset(url,asset);

            // 3) Create DZI file 
            await service.UpdateStatus(asset, (int) ImageProcessingStates.CreatingDZI);
            var res = ProcessFile(localUri);
            _logger.LogInformation("Processed file "+res);

            // 4) Upload it
            await service.UpdateStatus(asset, (int) ImageProcessingStates.Uploading);
            await _fileOps.UploadIndexFileAndDirectory(Path.ChangeExtension(localUri,".dzi"),
                                                       Path.ChangeExtension(localUri,".dzi").Replace(".dzi","_files/"),
                                                       asset);
                    
            // 5) delete local files 
            _logger.LogInformation("about to delete files");
            Directory.Delete(Path.GetDirectoryName(localUri), true);

            // 6) Mark it as completed            
            await service.UpdateStatus(asset, (int) ImageProcessingStates.Processed);
        }

        #endregion

        private void VipsStartup() {
            if (!ModuleInitializer.VipsInitialized) {
                _logger.LogCritical("failed to init vips");
            }
            else {
                _logger.LogInformation("Successfully started Libvips");
            }
            
            Log.SetLogHandler("VIPS",Enums.LogLevelFlags.All,(domain, level, message) => {
                switch (level) {
                    case Enums.LogLevelFlags.FlagRecursion:
                    case Enums.LogLevelFlags.FlagFatal:
                    case Enums.LogLevelFlags.Error:
                    case Enums.LogLevelFlags.Critical:
                        _logger.LogCritical(domain + Environment.NewLine + message);
                        break;
                    case Enums.LogLevelFlags.Warning:
                        _logger.LogWarning(domain + Environment.NewLine + message);
                        break;
                    case Enums.LogLevelFlags.Message:
                    case Enums.LogLevelFlags.Info:
                    case Enums.LogLevelFlags.Debug:
                    case Enums.LogLevelFlags.AllButFatal:
                    case Enums.LogLevelFlags.AllButRecursion:
                    case Enums.LogLevelFlags.All:
                    case Enums.LogLevelFlags.FlagMask:
                    case Enums.LogLevelFlags.LevelMask:
                        _logger.LogInformation(domain + Environment.NewLine + message);
                        break;
                }
            });
            // use memory checking
            Base.LeakSet(1);
        }

        public string ProcessFile(string file, string suffix = ".png", int tileSize = 256, int overlap = 1) {
            if (!File.Exists(file)) {
                _logger.LogError("file not found " + file);
                throw new ArgumentException("file not found ", nameof(file));
            }

            _logger.LogWarning("About to run DZI on " + file);
            try {
                var outputFolder =
                    Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file));

                using (var image = Image.NewFromFile(file, access: Enums.Access.Sequential)) {                    

                    image.Dzsave(outputFolder, suffix: suffix, tileSize: tileSize, overlap: overlap);
                    
                }
                _logger.LogWarning("Successfully created DZI for " + file);
                outputFolder = outputFolder + "_files";
                
                return outputFolder;
            }
            catch (Exception e) {
                _logger.LogCritical(e, "failed to run DZI " + file);
                throw;
            }

        }

    }
}