﻿using Siwait.Phone.Shared.Controllers.Identity;
using Siwait.Phone.Shared.Dtos.Identity;

namespace Siwait.Phone.Client.Core.Components.Pages.Authorized.Settings;

public partial class SessionsSection
{
    private bool isWaiting;
    private Guid? currentSessionId;
    private UserSessionDto? currentSession;
    private UserSessionDto[] otherSessions = [];

    [AutoInject] private IUserController userController = default!;


    protected override async Task OnInitAsync()
    {
        await LoadSessions();

        await base.OnInitAsync();
    }


    private async Task LoadSessions()
    {
        List<UserSessionDto> userSessions = [];
        currentSessionId = await PrerenderStateService.GetValue(async () => (await AuthenticationStateTask).User.GetSessionId());

        try
        {
            userSessions = await userController.GetUserSessions(CurrentCancellationToken);
        }
        finally
        {
            otherSessions = userSessions.Where(s => s.SessionUniqueId != currentSessionId).ToArray();
            currentSession = userSessions.SingleOrDefault(s => s.SessionUniqueId == currentSessionId);
        }
    }

    private async Task RevokeSession(UserSessionDto session)
    {
        if (isWaiting || session.SessionUniqueId == currentSessionId) return;

        isWaiting = true;

        try
        {
            await userController.RevokeSession(session.SessionUniqueId, CurrentCancellationToken);

            SnackBarService.Success(Localizer[nameof(AppStrings.RemoveSessionSuccessMessage)]);
            await LoadSessions();
        }
        catch (KnownException e)
        {
            SnackBarService.Error(e.Message);
        }
        finally
        {
            isWaiting = false;
        }
    }

    private static string GetImageUrl(string? device)
    {
        if (string.IsNullOrEmpty(device)) return "unknown.png";

        var d = device.ToLowerInvariant();

        if (d.Contains("win") /*Windows, WinUI, Win32*/) return "windows.png";

        if (d.Contains("android")) return "android.png";

        if (d.Contains("linux")) return "linux.png";

        return "apple.png";
    }

    private BitPersonaPresence GetPresence(DateTimeOffset renewedOn)
    {
        return DateTimeOffset.UtcNow - renewedOn < TimeSpan.FromMinutes(5) ? BitPersonaPresence.Online
                    : DateTimeOffset.UtcNow - renewedOn < TimeSpan.FromMinutes(15) ? BitPersonaPresence.Away
                    : BitPersonaPresence.Offline;
    }

    private string GetLastSeenOn(DateTimeOffset renewedOn)
    {
        return DateTimeOffset.UtcNow - renewedOn < TimeSpan.FromMinutes(5) ? Localizer[nameof(AppStrings.Online)]
                    : DateTimeOffset.UtcNow - renewedOn < TimeSpan.FromMinutes(15) ? Localizer[nameof(AppStrings.Recently)]
                    : renewedOn.ToLocalTime().ToString("g");
    }
}
