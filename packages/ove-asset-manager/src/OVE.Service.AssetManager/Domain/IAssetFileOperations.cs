using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using OVE.Service.AssetManager.Models;

namespace OVE.Service.AssetManager.Domain {
    /// <summary>
    /// An interface for dealing with the actual file of an Asset
    /// </summary>
    public interface IAssetFileOperations {
        string ResolveFileUrl(OVEAssetModel asset);
        // ReSharper disable twice UnusedParameter.Global
        Task<bool> Move(OVEAssetModel oldAsset, OVEAssetModel newAsset);
        Task<bool> Delete(OVEAssetModel asset);
        Task<bool> Save(OVEAssetModel asset, IFormFile upload);
    }
}