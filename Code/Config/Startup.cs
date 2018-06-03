﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Bonsai.Areas.Front.Logic;
using Bonsai.Areas.Front.Logic.Auth;
using Bonsai.Areas.Front.Logic.Relations;
using Bonsai.Code.Infrastructure;
using Bonsai.Code.Services;
using Bonsai.Code.Services.Elastic;
using Bonsai.Code.Tools;
using Bonsai.Data;
using Bonsai.Data.Models;
using Bonsai.Data.Utils;
using Bonsai.Data.Utils.Seed;
using Dapper;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nest;
using Newtonsoft.Json;

namespace Bonsai.Code.Config
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddUserSecrets<Startup>()
                .AddEnvironmentVariables();

            Configuration = builder.Build();
            Environment = env;
        }

        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        /// <summary>
        /// Registers all required services in the dependency injection container.
        /// </summary>
        public void ConfigureServices(IServiceCollection services)
        {
            ConfigureMvcServices(services);
            ConfigureAuthServices(services);
            ConfigureDatabaseServices(services);
            ConfigureElasticServices(services);

            services.AddTransient<MarkdownService>();
            services.AddTransient<AppConfigService>();
            services.AddTransient<RelationsPresenterService>();
            services.AddTransient<PagePresenterService>();
            services.AddTransient<MediaPresenterService>();
            services.AddTransient<CalendarPresenterService>();
            services.AddTransient<SearchPresenterService>();
            services.AddTransient<AuthService>();
        }

        /// <summary>
        /// Configures the web framework pipeline.
        /// </summary>
        public void Configure(IApplicationBuilder app)
        {
            using (var scope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                var context = scope.ServiceProvider.GetService<AppDbContext>();
                var elastic = scope.ServiceProvider.GetService<ElasticService>();

                context.EnsureDatabaseCreated();

                if(Environment.IsDevelopment())
                    SeedData.EnsureSeeded(context, elastic);
            }

            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage()
                   .UseBrowserLink();
            }

            if (Environment.IsProduction())
            {
                app.UseRewriter(new RewriteOptions().AddRedirectToHttps());
            }

            app.UseStaticFiles()
               .UseAuthentication()
               .UseSession()
               .UseMvc();
        }

        /// <summary>
        /// Configures and registers MVC-related services.
        /// </summary>
        private void ConfigureMvcServices(IServiceCollection services)
        {
            services.AddMvc()
                    .AddControllersAsServices()
                    .AddSessionStateTempDataProvider()
                    .AddJsonOptions(opts =>
                    {
                        var convs = new List<JsonConverter>
                        {
                            new FuzzyDate.FuzzyDateJsonConverter(),
                            new FuzzyRange.FuzzyRangeJsonConverter()
                        };

                        convs.ForEach(opts.SerializerSettings.Converters.Add);

                        JsonConvert.DefaultSettings = () =>
                        {
                            var settings = new JsonSerializerSettings();
                            convs.ForEach(settings.Converters.Add);
                            return settings;
                        };
                    });

            services.AddRouting(opts =>
            {
                opts.AppendTrailingSlash = false;
                opts.LowercaseUrls = false;
            });

            services.AddSession();

            services.AddScoped<IActionContextAccessor, ActionContextAccessor>();
            services.AddScoped<IUrlHelper>(x => new UrlHelper(x.GetService<IActionContextAccessor>().ActionContext));

            if (Environment.IsProduction())
            {
                services.Configure<MvcOptions>(opts =>
                {
                    opts.Filters.Add(new RequireHttpsAttribute());
                });
            }
        }

        /// <summary>
        /// Configures the auth-related sessions.
        /// </summary>
        private void ConfigureAuthServices(IServiceCollection services)
        {
            services.AddAuthorization(opts =>
            {
                opts.AddPolicy(AuthRequirement.Name, p =>
                {
                    p.AuthenticationSchemes = new List<string> { AuthService.ExternalCookieAuthType };
                    p.Requirements.Add(new AuthRequirement());
                });
            });

            services.AddSingleton<IAuthorizationHandler, AuthHandler>();

            services.AddAuthentication(AuthService.ExternalCookieAuthType)
                    .AddFacebook(opts =>
                    {
                        opts.SignInScheme = AuthService.ExternalCookieAuthType;
                        opts.AppId = Configuration["Auth:Facebook:AppId"];
                        opts.AppSecret = Configuration["Auth:Facebook:AppSecret"];

                        foreach(var scope in new [] { "email", "user_birthday", "user_gender" })
                            opts.Scope.Add(scope);
                    })
                    .AddGoogle(opts =>
                    {
                        opts.SignInScheme = AuthService.ExternalCookieAuthType;
                        opts.ClientId = Configuration["Auth:Google:ClientId"];
                        opts.ClientSecret = Configuration["Auth:Google:ClientSecret"];

                        foreach(var scope in new [] { "email", "profile" })
                            opts.Scope.Add(scope);
                    })
                    .AddCookie(AuthService.ExternalCookieAuthType, opts =>
                    {
                        opts.LoginPath = "/auth/login";
                    });
        }

        /// <summary>
        /// Configures and registers database-related services.
        /// </summary>
        private void ConfigureDatabaseServices(IServiceCollection services)
        {
            services.AddTransient<AppDbContext>();
            services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(Configuration.GetConnectionString("Database")));

            services.AddIdentity<AppUser, IdentityRole>()
                    .AddEntityFrameworkStores<AppDbContext>()
                    .AddDefaultTokenProviders();

            SqlMapper.AddTypeHandler(new FuzzyDate.FuzzyDateTypeHandler());
            SqlMapper.AddTypeHandler(new FuzzyDate.NullableFuzzyDateTypeHandler());
            SqlMapper.AddTypeHandler(new FuzzyRange.FuzzyRangeTypeHandler());
            SqlMapper.AddTypeHandler(new FuzzyRange.NullableFuzzyRangeTypeHandler());
        }

        /// <summary>
        /// Registers ElasticSearch-related services.
        /// </summary>
        private void ConfigureElasticServices(IServiceCollection services)
        {
            var host = Configuration["ElasticSearch:Host"];
            var settings = new ConnectionSettings(new Uri(host)).DisableAutomaticProxyDetection()
                                                                .DisablePing();

            services.AddScoped(s => new ElasticClient(settings));
            services.AddScoped<ElasticService>();
        }
    }
}
