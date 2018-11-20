using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OVE.Service.AssetManager.Models;
using System.IO.Compression;
using Newtonsoft.Json;

namespace OVE.Service.AssetManager.Domain {
    public class S3AssetFileOperations : IAssetFileOperations {

        private readonly ILogger<S3AssetFileOperations> _logger;
        private readonly IConfiguration _configuration;

        private const string S3ClientAccessKey = "s3Client:AccessKey";
        private const string S3ClientSecret = "s3Client:Secret";
        private const string S3ClientServiceUrl = "s3Client:ServiceURL";

        public S3AssetFileOperations(ILogger<S3AssetFileOperations> logger, IConfiguration configuration) {
            _logger = logger;
            _configuration = configuration;
        }

        private IAmazonS3 GetS3Client(IConfiguration configuration) {

            IAmazonS3 s3Client = new AmazonS3Client(
                configuration.GetValue<string>(S3ClientAccessKey),
                configuration.GetValue<string>(S3ClientSecret),
                new AmazonS3Config {
                    ServiceURL = configuration.GetValue<string>(S3ClientServiceUrl).EnsureTrailingSlash(),
                    UseHttp = true,
                    ForcePathStyle = true
                }
            );
            _logger.LogInformation("Created new S3 Client");
            return s3Client;
        }

        #region Implementation of IFileOperations

        public string ResolveFileUrl(OVEAssetModel asset) {
            var url = _configuration.GetValue<string>(S3ClientServiceUrl).EnsureTrailingSlash()
                      + asset.Project + "/" + asset.StorageLocation;
            return url;
        }

#pragma warning disable 1998
        public async Task<bool> Move(OVEAssetModel oldAsset, OVEAssetModel newAsset) {
#pragma warning restore 1998
            //todo this is hard because we might have to move between s3 buckets and that is complex
            // https://stackoverflow.com/questions/9664904/best-way-to-move-files-between-s3-buckets
            throw new NotImplementedException();
        }

        public async Task<bool> Delete(OVEAssetModel asset) {
            _logger.LogInformation("about to delete file " + asset);
            try {
                using (var s3Client = GetS3Client(_configuration)) {
                    // delete folder containing the file and everything

                    ListObjectsResponse files = null;
                    while (files == null || files.S3Objects.Any()) {
                        if (files != null && files.S3Objects.Any()) {
                            foreach (var o in files.S3Objects) {
                                await s3Client.DeleteObjectAsync(asset.Project, o.Key);
                            }
                        }

                        // find more files
                        files = await s3Client.ListObjectsAsync(new ListObjectsRequest {
                            BucketName = asset.Project,
                            Prefix = asset.GetStorageGuid()
                        });

                    }

                    // if the bucket is empty then delete it 
                    var res = await s3Client.ListObjectsAsync(asset.Project);
                    if (!res.S3Objects.Any()) {
                        await s3Client.DeleteBucketAsync(asset.Project);
                    }
                }

                _logger.LogInformation("deleted file on s3 correctly");
                return true;
            }
            catch (Exception e) {
                _logger.LogError(e, "Failed to delete an s3 file for " + asset);
                return false;
            }
        }

        public async Task<bool> Save(OVEAssetModel asset, IFormFile upload) {
            _logger.LogInformation("about to upload " + asset);

            // set up the filename            
            var ext = ValidateExtension(Path.GetExtension(upload.FileName).ToLower());
            asset.StorageLocation = Guid.NewGuid() + "/" +
                                    S3Sanitize(Path.GetFileNameWithoutExtension(upload.FileName), ext) + ext;

            try {

                using (var s3Client = GetS3Client(_configuration)) {

                    // find or create the bucket
                    var buckets = await s3Client.ListBucketsAsync();
                    if (buckets.Buckets.FirstOrDefault(b => b.BucketName == asset.Project) == null) {
                        var res = await s3Client.PutBucketAsync(asset.Project);
                        if (res.HttpStatusCode != HttpStatusCode.OK) {
                            throw new Exception("could not create bucket" + asset.Project);
                        }

                        var openBucketPolicy =
                            "{\"Version\":\"2012-10-17\",\"Statement\":[{\"Effect\":\"Allow\",\"Principal\":{\"AWS\":[\"*\"]},\"Action\":[\"s3:GetBucketLocation\",\"s3:ListBucket\"],\"Resource\":[\"arn:aws:s3:::" +
                            asset.Project +
                            "\"]},{\"Effect\":\"Allow\",\"Principal\":{\"AWS\":[\"*\"]},\"Action\":[\"s3:GetObject\"],\"Resource\":[\"arn:aws:s3:::" +
                            asset.Project + "/*\"]}]}";

                        await s3Client.PutBucketPolicyAsync(asset.Project, openBucketPolicy);

                        _logger.LogInformation("Created bucket " + asset.Project);
                    }

                    // upload the asset
                    await Upload(s3Client, asset.Project, asset.StorageLocation, upload);

                    // is it a zip file? 
                    if (asset.StorageLocation.EndsWith(".zip")) {
                        _logger.LogInformation("about to unzip and asset " + asset.StorageLocation);
                        var files = await UnZipAsset(s3Client, asset, upload);
                        _logger.LogInformation("unzipped " + files + " files to the object store" +
                                               asset.StorageLocation);
                        // save the meta data
                        asset.AssetMeta = JsonConvert.SerializeObject(files);
                    }
                }

                _logger.LogInformation("uploaded " + asset);
                return true;
            }
            catch (Exception e) {
                _logger.LogError(e, "Failed to upload file for " + asset);
                return false;
            }

        }

        private async Task<List<string>> UnZipAsset(IAmazonS3 s3Client, OVEAssetModel asset, IFormFile upload) {
            string prefixFolder = asset.StorageLocation.Split("/").FirstOrDefault() + "/";

            List<string> filesUploaded = new List<string>();

            using (var zipFile = upload.OpenReadStream()) {
                var archive = new ZipArchive(zipFile);
                foreach (var entry in archive.Entries.Where(f => f.FullName.Contains("."))) {
                    // only files
                    var location = UnzipLocation(entry.FullName, prefixFolder);

                    using (var file = entry.Open()) {
                        //raw unzipped streams do not have length so copy to memory stream
                        using (var ms = new MemoryStream()) {
                            await file.CopyToAsync(ms);
                            await Upload(s3Client, asset.Project, location, ms);
                        }
                    }

                    filesUploaded.Add(location);
                }
            }

            return filesUploaded;
        }

        private string UnzipLocation(string entry, string prefixFolder) {
            var originalExt = Path.GetExtension(entry);
            var ext = ValidateExtension(originalExt.ToLower());
            var location = prefixFolder + S3Sanitize(entry.Replace(originalExt, ""), ext) + ext;
            return location;
        }

        private async Task Upload(IAmazonS3 s3Client, string bucketName, string assetStorageLocation,
            IFormFile upload) {
            // open and upload it 
            using (var file = upload.OpenReadStream()) {
                await Upload(s3Client, bucketName, assetStorageLocation, file);
            }
        }

        private async Task Upload(IAmazonS3 s3Client, string bucketName, string assetStorageLocation,
            Stream file) {
            using (var fileTransferUtility = new TransferUtility(s3Client)) {
                // upload the file 
                await fileTransferUtility.UploadAsync(file, bucketName, assetStorageLocation);
            }
        }

        /// <summary>
        /// Sanitize the file extension of a file
        /// files are not forced to have extensions though it is recommended
        /// </summary>
        /// <param name="input">input</param>
        /// <returns>sanitized version</returns>
        private string ValidateExtension(string input) {
            // filter to valid chars 
            Regex r = new Regex("^[a-zA-Z0-9]+$");
            input = input.Where(l => r.IsMatch(l.ToString())).Aggregate("", (acc, c) => acc + c);
          
            return "." + input;
        }

        /// <summary>
        /// Safely Sanitize the filename for s3
        /// Support multiple folders with / or \
        /// https://docs.aws.amazon.com/AmazonS3/latest/dev/UsingMetadata.html
        /// </summary>
        /// <param name="input">input file name</param>
        /// <param name="extension">file extension</param>
        /// <returns>sanitized version</returns>
        private string S3Sanitize(string input, string extension) {
            try {

                if (string.IsNullOrWhiteSpace(input)) {
                    throw new ArgumentNullException(nameof(input));
                }

                const int maxLength = 1024;// AWS limit
                input = input.Substring(0,Math.Min( input.Length, maxLength - extension.Length)); 

                // ensure that slashes face the right way 
                input = input.Replace("\\", "/");
                // filter to valid chars 
                Regex r = new Regex("^[-a-zA-Z0-9()_/]+$");
                input = input.Where(l => r.IsMatch(l.ToString())).Aggregate("", (acc, c) => acc + c);

                //remove empty folders
                while (input.Contains("//")) {
                    input = input.Replace("//", "/");
                }

                // stop empty file names 
                if (input.Length == 0) {
                    throw new ArgumentNullException(nameof(input));
                }

            }
            catch (Exception e) {
                _logger.LogError(e, "failed to sanitize s3 name " + input);
                return Guid.NewGuid().ToString();
            }

            return input;
        }

        #endregion
    }
}
