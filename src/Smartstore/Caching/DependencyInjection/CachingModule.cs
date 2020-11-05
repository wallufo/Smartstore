﻿using Autofac;
using Smartstore.Threading;

namespace Smartstore.Caching.DependencyInjection
{
    public class CachingModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<RequestCache>()
                .As<IRequestCache>()
                .InstancePerLifetimeScope();

            builder.RegisterType<CacheScopeAccessor>()
                .As<ICacheScopeAccessor>()
                .InstancePerLifetimeScope();

            builder.RegisterType<MemoryCacheStore>()
                .As<ICacheStore>()
                .As<IMemoryCacheStore>()
                .SingleInstance();

            builder.RegisterType<DefaultCacheFactory>()
                .As<ICacheFactory>()
                .SingleInstance();

            builder.RegisterType<HybridCacheManager>()
                .As<ICacheManager>()
                .SingleInstance();

            builder.RegisterType<DefaultAsyncState>()
                .As<IAsyncState>()
                .OnPreparing(e =>
                {
                    // Inject mem cache by default
                    e.Parameters = new[] { TypedParameter.From(e.Context.Resolve<IMemoryCacheStore>()) };
                })
                .SingleInstance();
        }
    }
}