namespace OVE.Service.Archives.Domain {

    public enum ArchiveProcessingStates {
        Error = -1,
        Unprocessed = 0,
        Processing = 1,
        Uploading = 2,
        Processed = 3
    }

}