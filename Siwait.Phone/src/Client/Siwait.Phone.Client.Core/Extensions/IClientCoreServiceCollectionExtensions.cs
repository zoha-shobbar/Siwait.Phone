﻿using Siwait.Phone.Client.Core;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Components.WebAssembly.Services;
using Siwait.Phone.Client.Core.Components;
using Siwait.Phone.Client.Core.Services.HttpMessageHandlers;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class IClientCoreServiceCollectionExtensions
{
    public static IServiceCollection AddClientCoreProjectServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Services being registered here can get injected in client side (Web, Android, iOS, Windows, macOS) and server side (during pre rendering)
        services.AddSharedProjectServices(configuration);

        services.AddTransient<IPrerenderStateService, NoopPrerenderStateService>();

        services.AddScoped<ThemeService>();
        services.AddScoped<CultureService>();
        services.AddScoped<HttpClientHandler>();
        services.AddScoped<LazyAssemblyLoader>();
        services.AddScoped<IAuthTokenProvider, ClientSideAuthTokenProvider>();
        services.AddScoped<IExternalNavigationService, DefaultExternalNavigationService>();

        // The following services must be unique to each app session.
        // Defining them as singletons would result in them being shared across all users in Blazor Server and during pre-rendering.
        // To address this, we use the AddSessioned extension method.
        // AddSessioned applies AddSingleton in BlazorHybrid and AddScoped in Blazor WebAssembly and Blazor Server, ensuring correct service lifetimes for each environment.
        services.AddSessioned<PubSubService>();
        services.AddSessioned<SnackBarService>();
        services.AddSessioned<MessageBoxService>();
        services.AddSessioned<ILocalHttpServer, NoopLocalHttpServer>();
        services.AddSessioned<ITelemetryContext, AppTelemetryContext>();
        services.AddSessioned<AuthenticationStateProvider, AuthenticationManager>();
        services.AddSessioned(sp => (AuthenticationManager)sp.GetRequiredService<AuthenticationStateProvider>());

        services.AddSingleton(sp => configuration.Get<ClientCoreSettings>()!);

        services.AddOptions<ClientCoreSettings>()
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddBitButilServices();
        services.AddBitBlazorUIServices();

        // This code constructs a chain of HTTP message handlers. By default, it uses `HttpClientHandler` 
        // to send requests to the server. However, you can replace `HttpClientHandler` with other HTTP message 
        // handlers, such as ASP.NET Core's `HttpMessageHandler` from the Test Host, which is useful for integration tests.
        services.AddScoped<HttpMessageHandlersChainFactory>(serviceProvider => transportHandler =>
        {
            var constructedHttpMessageHandler = ActivatorUtilities.CreateInstance<RequestHeadersDelegationHandler>(serviceProvider,
                        [ActivatorUtilities.CreateInstance<AuthDelegatingHandler>(serviceProvider,
                        [ActivatorUtilities.CreateInstance<RetryDelegatingHandler>(serviceProvider,
                        [ActivatorUtilities.CreateInstance<ExceptionDelegatingHandler>(serviceProvider, [transportHandler])])])]);
            return constructedHttpMessageHandler;
        });
        services.AddScoped<AuthDelegatingHandler>();
        services.AddScoped<RetryDelegatingHandler>();
        services.AddScoped<ExceptionDelegatingHandler>();
        services.AddScoped<RequestHeadersDelegationHandler>();
        services.AddScoped(serviceProvider =>
        {
            var transportHandler = serviceProvider.GetRequiredService<HttpClientHandler>();
            var constructedHttpMessageHandler = serviceProvider.GetRequiredService<HttpMessageHandlersChainFactory>().Invoke(transportHandler);
            return constructedHttpMessageHandler;
        });



        services.AddTypedHttpClients();

        return services;
    }

    internal static IServiceCollection AddSessioned<TService, TImplementation>(this IServiceCollection services)
        where TImplementation : class, TService
        where TService : class
    {
        if (AppPlatform.IsBlazorHybrid)
        {
            return services.AddSingleton<TService, TImplementation>();
        }
        else
        {
            return services.AddScoped<TService, TImplementation>();
        }
    }

    internal static IServiceCollection AddSessioned<TService>(this IServiceCollection services, Func<IServiceProvider, TService> implementationFactory)
        where TService : class
    {
        if (AppPlatform.IsBlazorHybrid)
        {
            services.Add(ServiceDescriptor.Singleton(implementationFactory));
        }
        else
        {
            services.Add(ServiceDescriptor.Scoped(implementationFactory));
        }

        return services;
    }

    internal static void AddSessioned<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection services)
        where TService : class
    {
        if (AppPlatform.IsBlazorHybrid)
        {
            services.AddSingleton<TService, TService>();
        }
        else
        {
            services.AddScoped<TService, TService>();
        }
    }
}
