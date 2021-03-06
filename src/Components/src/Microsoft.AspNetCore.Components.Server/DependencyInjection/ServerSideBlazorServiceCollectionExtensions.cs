// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Components.Hosting;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Components.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods to configure an <see cref="IServiceCollection"/> for Server-Side Blazor.
    /// </summary>
    public static class ServerSideBlazorServiceCollectionExtensions
    {
        /// <summary>
        /// Adds Server-Side Blazor services to the service collection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddServerSideBlazor(
            this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            return AddServerSideBlazor(services, null);
        }

        /// <summary>
        /// Adds Server-Side Blazor services to the service collection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <param name="startupType">A Blazor startup type.</param>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddServerSideBlazor(
            this IServiceCollection services,
            Type startupType)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (startupType == null)
            {
                throw new ArgumentNullException(nameof(startupType));
            }

            return AddServerSideBlazorCore(services, startupType);
        }

        /// <summary>
        /// Adds Server-Side Blazor services to the service collection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/>.</param>
        /// <typeparam name="TStartup">A Blazor startup type.</typeparam>
        /// <returns>The <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddServerSideBlazor<TStartup>(
            this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            return AddServerSideBlazorCore(services, typeof(TStartup));
        }

        private static IServiceCollection AddServerSideBlazorCore(
            IServiceCollection services,
            Type startupType)
        {
            AddStandardServerSideBlazorServices(services);

            if (startupType != null)
            {
                // Call TStartup's ConfigureServices method immediately
                var startup = Activator.CreateInstance(startupType);
                var wrapper = new ConventionBasedStartup(startup);
                wrapper.ConfigureServices(services);

                // Configure the circuit factory to call a startup action when each
                // incoming connection is established. The startup action is "call
                // TStartup's Configure method".
                services.Configure<DefaultCircuitFactoryOptions>(circuitFactoryOptions =>
                {
                    var endpoint = BlazorHub.DefaultPath; // TODO: allow configuring this
                    if (circuitFactoryOptions.StartupActions.ContainsKey(endpoint))
                    {
                        throw new InvalidOperationException(
                            "Multiple Server Side Blazor entries are configured to use " +
                            $"the same endpoint '{endpoint}'.");
                    }

                    circuitFactoryOptions.StartupActions.Add(endpoint, builder =>
                    {
                        wrapper.Configure(builder, builder.Services);
                    });
                });
            }

            return services;
        }

        private static void AddStandardServerSideBlazorServices(IServiceCollection services)
        {
            // Here we add a bunch of services that don't vary in any way based on the
            // user's configuration. So even if the user has multiple independent server-side
            // Blazor entrypoints, this lot is the same and repeated registrations are a no-op.
            services.TryAddSingleton<CircuitFactory, DefaultCircuitFactory>();
            services.TryAddScoped<ICircuitAccessor, DefaultCircuitAccessor>();
            services.TryAddScoped<Circuit>(s => s.GetRequiredService<ICircuitAccessor>().Circuit);
            services.TryAddScoped<IJSRuntimeAccessor, DefaultJSRuntimeAccessor>();
            services.TryAddScoped<IJSRuntime>(s => s.GetRequiredService<IJSRuntimeAccessor>().JSRuntime);
            services.TryAddScoped<IUriHelper, RemoteUriHelper>();

            // We've discussed with the SignalR team and believe it's OK to have repeated
            // calls to AddSignalR (making the nonfirst ones no-ops). If we want to change
            // this in the future, we could change AddServerSideBlazor to be an extension
            // method on ISignalRServerBuilder so the developer always has to chain it onto
            // their own AddSignalR call. For now we're keeping it like this because it's
            // simpler for developers in common cases.
            services.AddSignalR().AddMessagePackProtocol();
        }
    }
}
