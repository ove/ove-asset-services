using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OVE.Service.AssetManager.Domain;
using OVE.Service.Core.Services;

namespace OVE.Service.AssetManager.Controllers
{
    /// <summary>
    /// An API to enable other OVE Services to register their capabilities with the Asset Service
    /// </summary>
    [ApiController]
    [FormatFilter]
    public class ServicesRegistryController : ControllerBase {
        private readonly ILogger<OVEAssetModelController> _logger;
        private readonly ServiceRepository _serviceRepository;

        public ServicesRegistryController(ILogger<OVEAssetModelController> logger, ServiceRepository serviceRepository) {
            _logger = logger;
            _serviceRepository = serviceRepository;
        }

        /// <summary>
        /// Register another service with the asset manager service
        /// </summary>
        /// <param name="service">the service to register</param>
        /// <returns>OK/error</returns>
        [HttpPost]
        [Route("/api/ServicesRegistry/Register")]
        public ActionResult RegisterService(OVEService service) {
            _logger.LogInformation("Received request to register service "+service.Name);
            _serviceRepository.UpdateService(service);
            return Ok();
        }

        /// <summary>
        /// Return details of the service registration
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("/api/ServicesRegistry/GetService")]
        public ActionResult<OVEService> GetService(string name) {
            return _serviceRepository.GetService(name);
        }

    }
}