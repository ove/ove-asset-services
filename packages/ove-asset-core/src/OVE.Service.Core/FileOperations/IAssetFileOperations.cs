using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using OVE.Service.Core.Assets;

namespace OVE.Service.Core.FileOperations {
    /// <summary>
    /// An interface for dealing with the actual file of an Asset
    /// </summary>
    public interface IAssetFileOperations {
        string ResolveFileUrl(OVEAssetModel asset);
        // ReSharper disable twice UnusedParameter.Global
        Task<bool> Move(OVEAssetModel oldAsset, OVEAssetModel newAsset);
        Task<bool> Delete(OVEAssetModel asset);
        Task<bool> Save(OVEAssetModel asset, IFormFile upload);
        Task Upload(string bucketName, string assetStorageLocation, Stream file);
        /// <summary>
        /// Upload an index file and a directory
        /// note the index file and directory will be placed in the root of the asset folder on the object store
        /// </summary>
        /// <param name="file">full path to the index file</param>
        /// <param name="directory">full path of the directory</param>
        /// <param name="asset">the asset</param>
        /// <returns>success / failure</returns>
        Task<bool> UploadIndexFileAndDirectory(string file, string directory, OVEAssetModel asset);
        /// <summary>
        /// Sanitize the file extension
        /// </summary>
        /// <param name="input">the fil extension</param>
        /// <returns>sanitized version</returns>
        string SanitizeExtension(string input);
        /// <summary>
        /// Validate a file name
        /// </summary>
        /// <param name="input">file name without extension</param>
        /// <param name="extension">the extension, which should be validated </param>
        /// <returns>a sanitized version of the file name</returns>
        string SanitizeFilename(string input, string extension);
    }
}