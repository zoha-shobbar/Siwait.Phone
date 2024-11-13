using Siwait.Phone.Shared.Dtos.PushNotification;

namespace Siwait.Phone.Client.Windows.Services;

public partial class WindowsPushNotificationService : PushNotificationServiceBase
{
    public override Task<DeviceInstallationDto> GetDeviceInstallation(CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
