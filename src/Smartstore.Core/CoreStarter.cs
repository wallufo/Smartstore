﻿using System;
using System.Web;
using Autofac;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Smartstore.Caching.DependencyInjection;
using Smartstore.Core.Common.DependencyInjection;
using Smartstore.Core.Configuration.DependencyInjection;
using Smartstore.Core.Data;
using Smartstore.Core.Data.DependecyInjection;
using Smartstore.Core.Localization.DependencyInjection;
using Smartstore.Core.Logging.DependencyInjection;
using Smartstore.Core.Logging.Serilog;
using Smartstore.Core.Seo.Services;
using Smartstore.Core.Stores.DependencyInjection;
using Smartstore.Engine;
using Smartstore.Engine.DependencyInjection;
using Smartstore.Events.DependencyInjection;

namespace Smartstore.Core
{
    public class CoreStarter : StarterBase
    {
        public override int Order => int.MinValue + 100;

        public override void ConfigureServices(IServiceCollection services, IApplicationContext appContext, bool isActiveModule)
        {
            var appConfig = appContext.AppConfiguration;

            services.AddHttpContextAccessor();

            //// EF 2nd level caching
            //services.AddEntityFrameworkCache(o => 
            //{
            //    o.DisableLogging = !appContext.HostEnvironment.IsDevelopment();
            //});

            // Application DbContext as pooled factory
            services.AddPooledApplicationDbContextFactory<SmartDbContext>(appContext);

            // TODO: (core) Configuration for MemoryCache?
            services.AddMemoryCache();

            services.AddMiniProfiler(o =>
            {
                // TODO: (more) Move to module and configure
            }).AddEntityFramework();
        }

        public override void ConfigureContainer(ContainerBuilder builder, IApplicationContext appContext, bool isActiveModule)
        {
            builder.RegisterModule(new WorkModule());
            builder.RegisterModule(new CachingModule());
            builder.RegisterModule(new DbHooksModule(appContext));
            builder.RegisterModule(new EventsModule(appContext));

            builder.RegisterModule(new CommonModule());
            builder.RegisterModule(new LoggingModule());
            builder.RegisterModule(new SettingsModule());
            builder.RegisterModule(new StoresModule());
            builder.RegisterModule(new LocalizationModule());
        }

        public override void ConfigureApplication(IApplicationBuilder app, IApplicationContext appContext)
        {
            app.UseMiddleware<SerilogHttpContextMiddleware>();

            app.UseMiniProfiler();
            app.UsePolyfillHttpContext();
        }

        public override void ConfigureRoutes(IApplicationBuilder app, IEndpointRouteBuilder routes, IApplicationContext appContext)
        {
            routes.MapDynamicControllerRoute<SeoSlugRouteValueTransformer>("{**slug:minlength(2)}");
            routes.MapControllerRoute(
                name: "areas",
                pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
            routes.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
        }
    }
}