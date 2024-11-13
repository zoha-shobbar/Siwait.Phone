﻿using Siwait.Phone.Server.Api.Models.PushNotification;
using Siwait.Phone.Shared.Dtos.PushNotification;
using Riok.Mapperly.Abstractions;

namespace Siwait.Phone.Server.Api.Mappers;

/// <summary>
/// More info at Server/Mappers/README.md
/// </summary>
[Mapper]
public static partial class PushNotificationMapper
{
    public static partial void Patch(this DeviceInstallationDto source, DeviceInstallation destination);
}