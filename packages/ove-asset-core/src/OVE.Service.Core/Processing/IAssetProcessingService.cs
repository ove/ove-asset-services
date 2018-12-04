using System;
using System.Threading.Tasks;
using OVE.Service.Core.Assets;

namespace OVE.Service.Core.Processing {
    /// <summary>
    /// Common Asset Processing Service routines
    /// </summary>
    /// <typeparam name="TV">Processing states enum</typeparam>
    public interface IAssetProcessingService<TV>
        where TV : struct, IConvertible { // todo when .net 7.3 reaches .net core this type constraint should be System.Enum then the casts to int can disappear. 
        Task<bool> UpdateStatus(OVEAssetModel asset, int state, string errors = null); // todo int should be TV
        string DownloadAsset(string url, OVEAssetModel asset);
        Task<string> GetAssetUri(OVEAssetModel asset);
    }
}