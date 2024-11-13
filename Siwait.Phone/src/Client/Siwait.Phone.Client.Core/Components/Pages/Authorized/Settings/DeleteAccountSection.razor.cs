﻿using Siwait.Phone.Shared.Controllers.Identity;

namespace Siwait.Phone.Client.Core.Components.Pages.Authorized.Settings;

public partial class DeleteAccountSection
{
    private bool isDialogOpen;


    [AutoInject] IUserController userController = default!;


    private async Task DeleteAccount()
    {
        await userController.Delete(CurrentCancellationToken);

        await AuthenticationManager.SignOut(CurrentCancellationToken);
    }
}
