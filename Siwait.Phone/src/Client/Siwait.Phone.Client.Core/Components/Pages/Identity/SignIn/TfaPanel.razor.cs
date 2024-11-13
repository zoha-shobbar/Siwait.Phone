﻿using Siwait.Phone.Shared.Dtos.Identity;

namespace Siwait.Phone.Client.Core.Components.Pages.Identity.SignIn;

public partial class TfaPanel
{
    [Parameter] public bool IsWaiting { get; set; }

    [Parameter] public SignInRequestDto Model { get; set; } = default!;

    [Parameter] public EventCallback OnSendTfaToken { get; set; }
}
