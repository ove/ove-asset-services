using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OVE.Service.AssetManager.DbContexts;
using OVE.Service.AssetManager.Domain;
using OVE.Service.Core.FileOperations;
using OVE.Service.Core.FileOperations.S3;
using OVE.Service.Core.Formatters;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Swashbuckle.AspNetCore.Swagger;

namespace OVE.Service.AssetManager {
    public class Startup {
        private readonly ILogger<Startup> _logger;
        
        public Startup(IConfiguration configuration,ILogger<Startup> logger) {
            _logger = logger;
            Configuration = configuration;
            _logger.LogInformation("Beginning Dependency injection");
        }

        private IConfiguration Configuration { get; }
        private static string _version = "v1";
        
        private const string MariaDbConnectionString = "MariaDB:ConnectionString";
        private const string MariaDbVersion = "MariaDB:Version";      

        internal static void GetVersionNumber() {
            // read version from package.json
            var packageJson = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"package.json");
            if (File.Exists(packageJson)) {
                var package = JObject.Parse(File.ReadAllText(packageJson));
                _version = package["version"].ToString();
            }
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services) {
            _logger.LogInformation("Starting to configure Services");

            //register a cors policy we can later configure to use
            services.AddCors(o => o.AddPolicy("AllowAll", builder => {
                builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            }));

            // make upload file size unlimited via gui (+ attribute on method to enable API unlimited)
            services.Configure<FormOptions>(x => {
                x.ValueLengthLimit =  int.MaxValue;
                x.MultipartBodyLengthLimit = int.MaxValue; // In case of multipart
            });

            // dependency injection of domain classes 
            services.AddSingleton(Configuration);
            services.AddSingleton<ServiceRepository>();
            services.AddTransient<IAssetFileOperations, S3AssetFileOperations>();

            // use mvc
            services.AddMvc(options => {
                    options.InputFormatters.Insert(0, new RawRequestBodyFormatter());
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .AddXmlSerializerFormatters().AddJsonOptions(options => {
                    options.SerializerSettings.Formatting = Formatting.Indented;
                }).AddRazorPagesOptions( o=> {
                    o.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute());
                });

            // add the db
            var connectionString = Configuration.GetValue<string>(MariaDbConnectionString);
            var version = Configuration.GetValue<string>(MariaDbVersion);
            _logger.LogInformation($"Configuring db with connection string {connectionString} and version {version}");
            services.AddDbContext<AssetModelContext>(
                options => {
                    options.UseMySql(connectionString,
                        mysqlOptions => {
                            mysqlOptions.ServerVersion(new Version(version),
                                ServerType.MariaDb);
                        }
                    );
                });

            // set up swagger
            services.AddSwaggerGen(options => {

                options.SwaggerDoc(_version, new Info {
                    Title = "OVE Asset Management Microservice",
                    Version = _version,
                    Description =
                        "The OVE Asset Management Microservice is used to upload and manage digital assets for use in the OVE ecosystem. " +
                        "This works within the OVE (Open Visualization Environment) is an open-source software stack, " +
                        "designed to be used in large scale visualization environments like the [Imperial College](http://www.imperial.ac.uk) " +
                        "[Data Science Institute\'s](http://www.imperial.ac.uk/data-science/) [Data Observatory](http://www.imperial.ac.uk/data-science/data-observatory/). " +
                        "OVE applications are applications designed to work with the OVE core. They are launched and managed within the browser-based OVE environment. " +
                        "Each OVE application exposes a standard control API and in some cases some application specific APIs.\"",
                    TermsOfService = "Terms Of Service",
                    Contact = new Contact {Email = "David.Birch@imperial.ac.uk"},
                    License = new License {Name = "MIT License", Url = "https://opensource.org/licenses/MIT"}


                });
                var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"OVE.Service.AssetManager.xml");
                options.IncludeXmlComments(filePath);
                options.DescribeAllEnumsAsStrings();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env) {
            // error pages
            if (env.IsDevelopment()) {
                app.UseDeveloperExceptionPage();
            }
            else {
                app.UseExceptionHandler("/Home/Error");
            }

            //set default content type
            app.UseStaticFiles(new StaticFileOptions {
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
                    c.SwaggerEndpoint("/swagger/" + _version + "/swagger.json", "Asset Service " + _version);
                    c.RoutePrefix = "api-docs";
                });
        }
    }
}
