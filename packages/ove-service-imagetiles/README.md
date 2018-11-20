# Image Tile Service

This service uses the [libvips](https://jcupitt.github.io/libvips/) library (via the [NetVips](https://github.com/kleisauke/net-vips) binding) to take Image based Assets and create [Deep Zoom](https://en.wikipedia.org/wiki/Deep_Zoom) image tilesets.


## Configuration

Configuration of the service is achieved by modifying the ``appsettings.json`` file. These settings are automatically overridden by **Environment Variables** (reflection of the JSON hierarchy should be achieved using ``__`` e.g. ``s3Client__Secret``). The number of Assets to process concurrently may be configured by changing the ``ImageProcessingConfig__MaxConcurrent`` variable. The interval to poll the Asset Manager for new Assets to process may be configured under ``ImageProcessingConfig__PollSeconds``.

### Configuring the Service

You must provide the service with the URL of the asset manager it should register with. Further, you should provide the final externally accessible URL of the service (used for callbacks from the Asset Manager).

```
  "AssetManagerHostUrl" :  "http://localhost:8181" ,
  "ServiceHostUrl" : "http://localhost:8182", 
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
 
 
