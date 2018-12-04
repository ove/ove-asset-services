using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OVE.Service.Core.Extensions;

namespace OVE.Service.Core.Services {
    public static class RegisterService {
        /// <summary>
        /// Register this OVE service with the Asset Manager Service 
        /// </summary>
        /// <param name="processingStates"></param>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        /// <param name="viewUrl"></param>
        public static async void WithAssetManager(Array processingStates, IConfiguration configuration,
            ILogger logger, string viewUrl) {

            // get the service description from the AppSettings.json 
            OVEService service = new OVEService();
            configuration.Bind("Service", service);
            service.ViewIFrameUrl = configuration.GetValue<string>("ServiceHostUrl").RemoveTrailingSlash() + viewUrl;

            // then update the real processing states
            service.ProcessingStates.Clear();
            foreach (var state in processingStates) {
                service.ProcessingStates.Add(((int) state).ToString(), state.ToString());
            }

            // register the service

            bool registered = false;
            while (!registered) {
                string url = null;
                try {
                    // permit environmental variables to be updated 
                    url = configuration.GetValue<string>("AssetManagerHostUrl").RemoveTrailingSlash() +
                          configuration.GetValue<string>("RegistrationApi");

                    logger.LogInformation($"About to register with url {url} we are on {service.ViewIFrameUrl}");

                    using (var client = new HttpClient()) {
                        var responseMessage = await client.PostAsJsonAsync(url, service);

                        logger.LogInformation($"Result of Registration was {responseMessage.StatusCode}");

                        registered = responseMessage.StatusCode == HttpStatusCode.OK;
                    }
                }
                catch (Exception e) {
                    logger.LogWarning($"Failed to register - exception was {e}");
                    registered = false;
                }

                if (!registered) {
                    logger.LogWarning($"Failed to register with an Asset Manager on {url}- trying again soon");
                    Thread.Sleep(10000);
                }
            }
        }
    }
}