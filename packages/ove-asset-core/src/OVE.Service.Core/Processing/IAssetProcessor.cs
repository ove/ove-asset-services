using System;
using System.Threading.Tasks;
using OVE.Service.Core.Assets;

namespace OVE.Service.Core.Processing {
   /// <summary>
   /// An interface to implement to process assets. 
   /// </summary>
   /// <typeparam name="TV">The Enum for the processing states</typeparam>
    public interface IAssetProcessor<TV> 
        where TV : struct, IConvertible { // todo this will be System.Enum in C# 7.3
        Task Process(IAssetProcessingService<TV> service, OVEAssetModel asset) ;
    }
}