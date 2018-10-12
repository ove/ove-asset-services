using Microsoft.AspNetCore.Mvc;

namespace OVE.Service.AssetManager.Domain {
    public static class ControllerExtensions {
        /// <summary>
        /// Enable a method to return either an Action View or a .json or .xml file as requested
        /// By default html is returned. 
        /// </summary>
        /// <typeparam name="T">type of result</typeparam>
        /// <param name="controller">controller</param>
        /// <param name="model">the result</param>
        /// <returns>either a view of the model or the model as xml or json as per request</returns>
        public static ActionResult<T> FormatOrView<T>(this Controller controller, T model) {
            var requestContentType = controller.Request.Headers["Accept"].ToString();

            return requestContentType.Contains("html") ? controller.View(model) : new ActionResult<T>(model);
        }
    }
}
