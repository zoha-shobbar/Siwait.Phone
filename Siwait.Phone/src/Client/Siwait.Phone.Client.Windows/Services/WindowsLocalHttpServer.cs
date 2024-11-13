﻿using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using EmbedIO;
using EmbedIO.Actions;
using Siwait.Phone.Client.Core.Components;
using Microsoft.Extensions.Logging;

namespace Siwait.Phone.Client.Windows.Services;

public partial class WindowsLocalHttpServer : ILocalHttpServer
{
    [AutoInject] private IConfiguration configuration;
    [AutoInject] private IExceptionHandler exceptionHandler;
    [AutoInject] private ILogger<ILocalHttpServer> logger = default!;

    private WebServer? localHttpServer;

    public int Start(CancellationToken cancellationToken)
    {
        var port = GetAvailableTcpPort();

        localHttpServer = new WebServer(o => o
            .WithUrlPrefix($"http://localhost:{port}")
            .WithMode(HttpListenerMode.Microsoft))
            .WithModule(new ActionModule(Urls.SignInPage, HttpVerbs.Get, async ctx =>
            {
                try
                {
                    var url = $"{configuration.GetServerAddress()}/api/Identity/SocialSignedIn?culture={CultureInfo.CurrentUICulture.Name}";

                    ctx.Redirect(url);

                    await App.Current.Dispatcher.InvokeAsync(() => App.Current.MainWindow.Activate());

                    await Routes.OpenUniversalLink(ctx.Request.Url.PathAndQuery, replace: true);
                }
                catch (Exception exp)
                {
                    exceptionHandler.Handle(exp);
                }
            }));

        localHttpServer.HandleHttpException(async (context, exception) =>
        {
            exceptionHandler.Handle(new HttpRequestException(exception.Message), new Dictionary<string, object?>()
            {
                { "StatusCode" , exception.StatusCode },
                { "RequestUri" , context.Request.Url },
            });
        });

        _ = localHttpServer.RunAsync(cancellationToken)
            .ContinueWith(task =>
            {
                if (task.Exception is not null)
                {
                    exceptionHandler.Handle(task.Exception);
                }
            }, cancellationToken);

        return port;
    }

    private int GetAvailableTcpPort()
    {
        using TcpListener l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}