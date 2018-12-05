namespace OVE.Service.NetworkTiles.Domain {

    public enum NetworkTilesProcessingStates {
        Error = -1,
        Unprocessed = 0,
        Processing = 1,
        Uploading = 2,
        Processed = 3
    }

}