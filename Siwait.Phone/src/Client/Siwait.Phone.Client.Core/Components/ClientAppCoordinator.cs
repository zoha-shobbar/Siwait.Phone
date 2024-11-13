using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;

namespace Siwait.Phone.Client.Core.Components;

/// <summary>
/// Manages the initialization and coordination of core services and settings within the client application.
/// This includes authentication state handling, telemetry setup, culture configuration, and optional
/// services such as SignalR connections, push notifications, and application insights.
/// </summary>
public partial class ClientAppCoordinator : AppComponentBase
{
    private HubConnection? hubConnection;
    [AutoInject] private Notification notification = default!;
    [AutoInject] private IPushNotificationService pushNotificationService = default!;
    [AutoInject] private Navigator navigator = default!;
    [AutoInject] private IJSRuntime jsRuntime = default!;
    [AutoInject] private IStorageService storageService = default!;
    [AutoInject] private ILogger<ClientAppCoordinator> logger = default!;
    [AutoInject] private AuthenticationManager authManager = default!;
    [AutoInject] private CultureInfoManager cultureInfoManager = default!;
    [AutoInject] private ILogger<AuthenticationManager> authLogger = default!;
    [AutoInject] private IBitDeviceCoordinator bitDeviceCoordinator = default!;

    protected override async Task OnInitAsync()
    {
        AuthenticationManager.AuthenticationStateChanged += AuthenticationStateChanged;

        if (InPrerenderSession is false)
        {
            TelemetryContext.UserAgent = await navigator.GetUserAgent();
            TelemetryContext.TimeZone = await jsRuntime.GetTimeZone();
            TelemetryContext.Culture = CultureInfo.CurrentCulture.Name;
            if (AppPlatform.IsBlazorHybrid is false)
            {
                TelemetryContext.OS = await jsRuntime.GetBrowserPlatform();
            }


            AuthenticationStateChanged(AuthenticationManager.GetAuthenticationStateAsync());
        }

        if (AppPlatform.IsBlazorHybrid)
        {
            if (CultureInfoManager.MultilingualEnabled)
            {
                cultureInfoManager.SetCurrentCulture(new Uri(NavigationManager.Uri).GetCulture() ??  // 1- Culture query string OR Route data request culture
                                                     await storageService.GetItem("Culture") ?? // 2- User settings
                                                     CultureInfo.CurrentUICulture.Name); // 3- OS settings
            }

            await SetupBodyClasses();
        }

        await base.OnInitAsync();
    }

    private async void AuthenticationStateChanged(Task<AuthenticationState> task)
    {
        try
        {
            var user = (await task).User;
            TelemetryContext.UserId = user.IsAuthenticated() ? user.GetUserId() : null;
            TelemetryContext.UserSessionId = user.IsAuthenticated() ? user.GetSessionId() : null;

            var data = TelemetryContext.ToDictionary();


            using var scope = authLogger.BeginScope(data);
            {
                authLogger.LogInformation("Authentication state changed.");
            }

            await pushNotificationService.RegisterDevice(CurrentCancellationToken);

            await ConnectSignalR();
        }
        catch (Exception exp)
        {
            ExceptionHandler.Handle(exp);
        }
    }

    private async Task ConnectSignalR()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
        }

        hubConnection = new HubConnectionBuilder()
            .WithAutomaticReconnect(new SignalRInfinitiesRetryPolicy())
            .WithUrl($"{HttpClient.BaseAddress}app-hub", options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = options.Transports is HttpTransportType.WebSockets;
                // Avoid enabling long polling or Server-Sent Events. Focus on resolving the issue with WebSockets instead.
                // WebSockets should be enabled on services like IIS or Cloudflare CDN, offering significantly better performance.
                options.AccessTokenProvider = async () => await AuthTokenProvider.GetAccessToken();
            })
            .Build();

        hubConnection.On<string>(SignalREvents.SHOW_MESSAGE, async (message) =>
        {
            if (await notification.IsNotificationAvailable())
            {
                // Show local notification
                // Note that this code has nothing to do with push notification.
                await notification.Show("Siwait.Phone", new() { Body = message });
            }
            else
            {
                SnackBarService.Show("Siwait.Phone", message);
            }

            // The following code block is not required for Bit.BlazorUI components to perform UI changes. However, it may be necessary in other scenarios.
            /*await InvokeAsync(async () =>
            {
                StateHasChanged();
            });*/

            // You can also leverage IPubSubService to notify other components in the application.
        });

        hubConnection.On<string>(SignalREvents.PUBLISH_MESSAGE, async (message) =>
        {
            logger.LogInformation("Message {Message} received from server.", message);
            PubSubService.Publish(message);
        });

        hubConnection.Closed += HubConnectionDisconnected;
        hubConnection.Reconnected += HubConnectionConnected;
        hubConnection.Reconnecting += HubConnectionDisconnected;

        await hubConnection.StartAsync(CurrentCancellationToken);

        await HubConnectionConnected(null);
    }

    private async Task HubConnectionConnected(string? connectionId)
    {
        TelemetryContext.IsOnline = true;
        PubSubService.Publish(ClientPubSubMessages.IS_ONLINE_CHANGED, true);
        logger.LogInformation("SignalR connection {ConnectionId} established.", connectionId);
    }

    private async Task HubConnectionDisconnected(Exception? exception)
    {
        TelemetryContext.IsOnline = false;
        PubSubService.Publish(ClientPubSubMessages.IS_ONLINE_CHANGED, false);

        if (exception is null)
        {
            logger.LogInformation("SignalR connection lost."); // Was triggered intentionally by either server or client.
        }
        else
        {
            if (exception is HubException && exception.Message.EndsWith(nameof(AppStrings.UnauthorizedException)))
            {
                await AuthenticationManager.RefreshToken();
            }

            logger.LogError(exception, "SignalR connection lost.");
        }
    }


    private async Task SetupBodyClasses()
    {
        var cssClasses = new List<string> { };

        if (AppPlatform.IsWindows)
        {
            cssClasses.Add("bit-windows");
        }
        else if (AppPlatform.IsMacOS)
        {
            cssClasses.Add("bit-macos");
        }
        else if (AppPlatform.IsIOS)
        {
            cssClasses.Add("bit-ios");
        }
        else if (AppPlatform.IsAndroid)
        {
            cssClasses.Add("bit-android");
        }

        var cssVariables = new Dictionary<string, string>
        {
        };

        await jsRuntime.ApplyBodyElementClasses(cssClasses, cssVariables);
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        AuthenticationManager.AuthenticationStateChanged -= AuthenticationStateChanged;

        if (hubConnection is not null)
        {
            hubConnection.Closed -= HubConnectionDisconnected;
            hubConnection.Reconnected -= HubConnectionConnected;
            hubConnection.Reconnecting -= HubConnectionDisconnected;
            await hubConnection.DisposeAsync();
        }

        await base.DisposeAsync(disposing);
    }
}
