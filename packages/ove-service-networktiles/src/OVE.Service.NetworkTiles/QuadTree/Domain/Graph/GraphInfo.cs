using System.Collections.Generic;

namespace OVE.Service.NetworkTiles.QuadTree.Domain.Graph {
    public class GraphInfo {
        public List<string> NodeMandatoryFields { get; set; }
        public List<string> NodeOtherFields { get; set; }
        public List<string> LinkKeys { get; set; }
        public RectDimension RectDim;
    }
}