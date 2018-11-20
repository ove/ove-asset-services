using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OVE.Service.AssetManager.DbContexts;
using OVE.Service.AssetManager.Domain;
using OVE.Service.AssetManager.Domain;

namespace OVE.Service.AssetManager.Controllers {
    /// <summary>
    /// API methods for interacting with Zip file archives 
    /// </summary>
    [FormatFilter]
    public class ArchiveController : Controller {

        private readonly AssetModelContext _context;
        private readonly ILogger<ArchiveController> _logger;
        private readonly IConfiguration _config;

        /// <summary>
        /// Create a new API controller for OVE Asset Models using Dependency Injection 
        /// </summary>
        /// <param name="context">Database Context</param>
        /// <param name="logger">logger</param>
        /// <param name="config">config</param>        
        public ArchiveController(AssetModelContext context, ILogger<ArchiveController> logger, IConfiguration config) {
            _context = context;
            _logger = logger;
            _config = config;
            _logger.LogInformation("started Asset Controller Started");
        }

        /// <summary>
        /// return a list of the files within the archive.
        /// </summary>
        /// <param name="id">the asset id</param>
        /// <returns>a json list of the asset files</returns>
        [HttpGet]
        [Route("/ArchiveController/Details/{id}.{format?}")]
        public async Task<ActionResult<string>> GetArchiveContents(string id) {
            if (id == null) {
                return NotFound();
            }

            var assetModel = await _context.AssetModels.FirstOrDefaultAsync(m => m.Id == id);
            if (assetModel == null) {
                return NotFound();
            }

            return this.FormatOrView(assetModel.AssetMeta);
        }

        /// <summary>
        /// return a view of what is within the archive 
        /// </summary>
        /// <param name="id">id of the asset</param>
        /// <returns>view of the archive</returns>
        [HttpGet]
        [Route("/ArchiveController/ViewDetails/{id}.{format?}")]
        public async Task<ActionResult> ArchiveDetails(string id) {
            if (id == null) {
                return NotFound();
            }

            var assetModel = await _context.AssetModels
                .FirstOrDefaultAsync(m => m.Id == id);
            if (assetModel == null) {
                return NotFound();
            }

            return View(assetModel);
        }
    }
}
