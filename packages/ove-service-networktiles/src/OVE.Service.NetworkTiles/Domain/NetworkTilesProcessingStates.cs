// ReSharper disable UnusedMember.Global
namespace OVE.Service.NetworkTiles.Domain {

    public enum NetworkTilesProcessingStates {
        Error = -1,
        Unprocessed = 0,
        Processing = 1,
        CreatingQuadTree = 2,
        Uploading = 3,
        Processed = 4
    }
}