using BotSharp.Core.Modules;
using DotNetToolkit.JwtHelper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Serialization;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerUI;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.IO;
using System.Linq;

namespace BotSharp.WebHost
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            this.modulesStartup = new ModulesStartup(configuration);
        }

        private readonly ModulesStartup modulesStartup;

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAllHeaders", builder =>
                {
                    builder.WithOrigins("http://localhost:3110")
                        .AllowCredentials()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });
            services.AddJwtAuth(Configuration);

            services.AddMvc(options =>
             {
                 options.RespectBrowserAcceptHeader = true;
             })
                .AddNewtonsoftJson(options =>
                 {
                     options.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
                     options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                 });

            //PlatformModuleAssembyLoader.LoadAssemblies(Configuration, assembly => mvcBuilder.AddApplicationPart(assembly));

            this.modulesStartup.ConfigureServices(services);

            services.AddSwaggerGen(c =>
            {
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
                {
                    In = ParameterLocation.Header,
                    Description = "Please insert JWT with Bearer schema. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    { new OpenApiSecurityScheme(){
                        Reference = new OpenApiReference{Type = ReferenceType.SecurityScheme ,Id = "Bearer"}
                    }, new List<string>(0) }
                });

                var info = Configuration.GetSection("Swagger").Get<OpenApiInfo>();
                c.SwaggerDoc(info.Version, info);

                var filePath = Path.Combine(AppContext.BaseDirectory, "BotSharp.RestApi.xml");
                if (File.Exists(filePath))
                {
                    c.IncludeXmlComments(filePath);
                }

                var xmlPath = Path.Combine(AppContext.BaseDirectory, "BotSharp.Platform.Dialogflow.xml");
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }

                c.OperationFilter<SwaggerFileUploadOperation>();
            }).AddSwaggerGenNewtonsoftSupport();

            // register platform dependency
            /*services.AddTransient<IBotEngine>((provider) =>
            {
                return instance;
            });*/
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors("AllowAllHeaders");

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                var info = Configuration.GetSection("Swagger").Get<OpenApiInfo>();

                c.SupportedSubmitMethods(SubmitMethod.Get, SubmitMethod.Post, SubmitMethod.Put, SubmitMethod.Patch, SubmitMethod.Delete);
                c.ShowExtensions();
                c.SwaggerEndpoint(Configuration.GetValue<String>("Swagger:Endpoint"), info.Title);
                c.RoutePrefix = String.Empty;
                c.DocumentTitle = info.Title;
                c.InjectStylesheet(Configuration.GetValue<String>("Swagger:Stylesheet"));

                Console.WriteLine();
                Console.WriteLine($"{info.Title} [{info.Version}] {info.License.Name}");
                Console.WriteLine($"{info.Description}");
                Console.WriteLine($"{info.Contact.Name}, {DateTime.UtcNow.ToString()}");
                Console.WriteLine();
            });

            app.Use(async (context, next) =>
            {
                string token = context.Request.Headers["Authorization"];
                if (!string.IsNullOrWhiteSpace(token) && (token = token.Split(' ').Last()).Length == 32)
                {
                    var config = (IConfiguration)AppDomain.CurrentDomain.GetData("Configuration");
                    context.Request.Headers["Authorization"] = token;

                    context.Request.Headers["Authorization"] = "Bearer " + JwtToken.GenerateToken(config, token);
                }

                await next.Invoke();
            });
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(routes=>
            {
                routes.MapControllers();
            });

            this.modulesStartup.Configure(app, env);

            AppDomain.CurrentDomain.SetData("DataPath", Path.Combine(env.ContentRootPath, "App_Data"));
            AppDomain.CurrentDomain.SetData("Configuration", Configuration);
            AppDomain.CurrentDomain.SetData("ContentRootPath", env.ContentRootPath);
            AppDomain.CurrentDomain.SetData("Assemblies", Configuration.GetValue<String>("Assemblies").Split(','));

            /*InitializationLoader loader = new InitializationLoader();
            loader.Env = env;
            loader.Config = Configuration;
            loader.Load();*/
        }
    }
}