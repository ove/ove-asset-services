using System.Collections.Generic;

namespace OVE.Service.Core.Services {
    /// <summary>
    /// Metadata about OVE Services registered with the Asset Manager 
    /// </summary>
    public class OVEService {
        
        /// <summary>
        /// Service Name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// List of file types permitted e.g. .png
        /// lowercase with period please. 
        /// </summary>
        public List<string> FileTypes { get; set; }

        /// <summary>
        /// Provide a url for viewing a given asset.
        /// Must include {id} which will be replaced with id of asset
        /// </summary>
        public string ViewIFrameUrl { get; set; }

        /// <summary>
        /// An Enum for converting integer processing states into friendly errors
        /// </summary>
        public Dictionary<string,string> ProcessingStates { get; set; }
        
    }
}