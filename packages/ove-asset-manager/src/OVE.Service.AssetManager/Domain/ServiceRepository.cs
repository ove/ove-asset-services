using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OVE.Service.Core.Assets;
using OVE.Service.Core.Services;

namespace OVE.Service.AssetManager.Domain {
    /// <summary>
    /// A singleton services for managing knowledge of other services.
    /// filled form appsetting.json and API updates from other services.
    /// </summary>
    public class ServiceRepository {
        private readonly ILogger<ServiceRepository> _logger;
        
        private readonly ConcurrentDictionary<string,OVEService> _knownServices =new ConcurrentDictionary<string, OVEService>();

        public ServiceRepository(ILogger<ServiceRepository> logger, IConfiguration configuration) {
            _logger = logger;

            List<OVEService> services = new List<OVEService>();
            configuration.Bind("OVEServices",services);

            foreach (var oveService in services) {
                _logger.LogInformation("found service from Config: "+oveService.Name);
                UpdateService(oveService);
            }
        }

        // ReSharper disable once UnusedMember.Global << used in view
        public List<SelectListItem> GetServices() {
            return _knownServices.Select(s => new SelectListItem(s.Key, s.Key)).Reverse().ToList();      
        }

        public bool ValidateServiceChoice(string serviceName, IFormFile upload) {
            var extension = Path.GetExtension(upload.FileName).ToLower();
            return ValidateServiceChoice(serviceName, extension);
        }

        public bool ValidateServiceChoice(string serviceName, string extension) {
            return _knownServices.ContainsKey(serviceName) && _knownServices[serviceName].FileTypes.Contains(Path.GetExtension(extension));
        }

        public void UpdateService(OVEService service) {
            _logger.LogInformation("Updated Service "+service.Name);
            this._knownServices.AddOrUpdate(service.Name, k => service, (k, v) => service);
        }

        public OVEService GetService(string name) {
            if (!_knownServices.TryGetValue(name, out var res)) {
                _logger.LogError("request for unknown service "+name);
            }
            return res;
        }

        /// <summary>
        /// Convert a numeric processing state into a user friendly message. 
        /// </summary>
        /// <param name="asset">asset</param>
        /// <returns>user friendly message</returns>
        // ReSharper disable once UnusedMember.Global << used in view
        public string TranslateProcessingState(OVEAssetModel asset) {
            var service = GetService(asset.Service);
            if (service == null) {
                return "Unknown Service";

            }
            if (service.ProcessingStates.TryGetValue(asset.ProcessingState.ToString(), out string message)) {
                return message;
            }

            return asset.ProcessingState == 0 ? "Unprocessed" : "unknown";
        }

        // ReSharper disable once UnusedMember.Global << used in view
        public string GetViewUrl(OVEAssetModel model) {
            var service = GetService(model.Service);
            if (service == null) {
                return "";
            }
            
            var url = service.ViewIFrameUrl;
            return string.IsNullOrWhiteSpace(url) 
                ? "" 
                : url.Replace("{id}", model.Id);
        }
    }
}
