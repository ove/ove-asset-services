using System.Collections.Generic;

namespace OVE.Service.Archives.Models {
    public class ArchiveViewModel {
        public string Id { get; set; }
        public string AssetUrl { get; set; }
        public string RootFile { get; set; }
        public List<string> Files { get;set; }
    }
}
