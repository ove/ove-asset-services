# Archive Service

This service unzips archive files onto the object store for use.


## Configuration

Configuration of the service is achieved by modifying the ``appsettings.json`` file. These settings are automatically overridden by **Environment Variables** (reflection of the JSON hierarchy should be achieved using ``__`` e.g. ``s3Client__Secret``). The number of Assets to process concurrently may be configured by changing the ``ArchiveProcessingConfig__MaxConcurrent`` variable. The interval to poll the Asset Manager for new Assets to process may be configured under ``ArchiveProcessingConfig__PollSeconds``.

### Configuring the Service

You must provide the service with the URL of the asset manager it should register with. Further, you should provide the final externally accessible URL of the service (used for callbacks from the Asset Manager).

```
  "AssetManagerHostUrl" :  "http://localhost:8181" ,
  "ServiceHostUrl" : "http://localhost:8183", 
```

### Configuring S3

In common with all S3 compatible object stores the following three properties are required and should be set as follows in `appsettings.json` or overridden by **Environment Variables** as discussed above.

```  
"s3Client": {
    "AccessKey": "key",
    "Secret": "secret",
    "ServiceURL": "host"
  }
 ```
 
 
