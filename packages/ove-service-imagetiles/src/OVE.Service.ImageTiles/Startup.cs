using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using OVE.Service.ImageTiles.Domain;
using Swashbuckle.AspNetCore.Swagger;

namespace OVE.Service.ImageTiles {
    public class Startup {
        public Startup(IConfiguration configuration) {
            Configuration = configuration;
        }

        private IConfiguration Configuration { get; }
        private static string _version = "v1";

        internal static void GetVersionNumber() {
            // read version from package.json
            var packagejson = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"package.json");
            if (File.Exists(packagejson)) {
                var package = JObject.Parse(File.ReadAllText(packagejson));
                _version = package["version"].ToString();
            }
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services) {
            //register a cors policy we can later configure to use
            services.AddCors(o => o.AddPolicy("AllowAll", builder => {
                builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            }));

            // make upload file size unlimited via gui (+ attribute on method to enable API unlimited)
            services.Configure<FormOptions>(x => {
                x.ValueLengthLimit = int.MaxValue;
                x.MultipartBodyLengthLimit = int.MaxValue; // In case of multipart
            });

            //start the processor microservice 
            services.AddHostedService<ASyncImageProcessor>();

            // dependency injection of domain classes 
            services.AddSingleton(Configuration);
            services.AddTransient<ImageProcessor>();

            // use mvc
            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .AddRazorPagesOptions( o=> {
                    o.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
            });

            // set up swagger
            services.AddSwaggerGen(options => {

                options.SwaggerDoc(_version, new Info {
                    Title = "OVE Image Tile Microservice",
                    Version = _version,
                    Description =
                        "The OVE Image Tile Microservice is used to process Image assets to produce tile services from them. " +
                        "This works within the OVE (Open Visualization Environment) is an open-source software stack, " +
                        "designed to be used in large scale visualization environments like the [Imperial College](http://www.imperial.ac.uk) " +
                        "[Data Science Institute\'s](http://www.imperial.ac.uk/data-science/) [Data Observatory](http://www.imperial.ac.uk/data-science/data-observatory/). " +
                        "OVE applications are applications designed to work with the OVE core. They are launched and managed within the browser-based OVE environment. " +
                        "Each OVE application exposes a standard control API and in some cases some application specific APIs.\"",
                    TermsOfService = "Terms Of Service",
                    Contact = new Contact {Email = "David.Birch@imperial.ac.uk"},
                    License = new License {Name = "MIT License", Url = "https://opensource.org/licenses/MIT"}


                });
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"OVE.Service.ImageTiles.xml");
                options.IncludeXmlComments(filePath);
                options.DescribeAllEnumsAsStrings();
            });

        }

        /// <summary>
        /// Register this OVE service with the Asset Manager Service 
        /// </summary>
        /// <param name="app">kestrel app</param>
        private async void RegisterServiceWithAssetManager(IApplicationBuilder app) {
            OVEService service = new OVEService();
            Configuration.Bind("Service", service);
            
            service.ViewIFrameUrl = Configuration.GetValue<string>("ServiceHostUrl").RemoveTrailingSlash() + "/api/ImageController/ViewImage/?id={id}"; 

            // update the real processing states
            service.ProcessingStates.Clear();
            foreach (var state in Enum.GetValues(typeof(ProcessingStates))) {
                service.ProcessingStates.Add(((int) state).ToString(), state.ToString());
            }

            // register the service

            string url = Configuration.GetValue<string>("AssetManagerHostUrl").RemoveTrailingSlash() +
                         Configuration.GetValue<string>("RegistrationApi");

            Console.WriteLine("About to register with url " + url + " we are on " + service.ViewIFrameUrl);

            using (var client = new HttpClient()) {
                var responseMessage = await client.PostAsJsonAsync(url, service);

                Console.WriteLine("Result of Registration was " + responseMessage.StatusCode);
            }

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env) {

            RegisterServiceWithAssetManager(app);

            // error pages
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            }
            else {
                app.UseExceptionHandler("/Home/Error");
            }

            //enable serving all file types (e.g. .dzi)
            app.UseStaticFiles(new StaticFileOptions {
                ServeUnknownFileTypes = true,
                DefaultContentType = "application/json"
            });

            // may not need
            app.UseCookiePolicy();

            // use our cors policy defined earlier
            app.UseCors("AllowAll");

            // use mvc and set up routes for apis 
            app.UseMvc(routes => {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            // turn swagger on
            app.UseSwagger()
                .UseSwaggerUI(c => {
                    c.SwaggerEndpoint("/swagger/" + _version + "/swagger.json", "TileService " + _version);
                    c.RoutePrefix = "api-docs";
                });
        }
    }
}
