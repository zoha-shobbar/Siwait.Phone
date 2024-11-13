using Siwait.Phone.Server.Api.Services;
using Siwait.Phone.Shared.Dtos.PushNotification;
using Siwait.Phone.Shared.Controllers.PushNotification;

namespace Siwait.Phone.Server.Api.Controllers.PushNotification;

[Route("api/[controller]/[action]")]
[ApiController, AllowAnonymous]
public partial class PushNotificationController : AppControllerBase, IPushNotificationController
{
    [AutoInject] PushNotificationService pushNotificationService = default!;

    [HttpPost]
    public async Task RegisterDevice([Required] DeviceInstallationDto deviceInstallation, CancellationToken cancellationToken)
    {
        await pushNotificationService.RegisterDevice(deviceInstallation, cancellationToken);
    }

    [HttpPost("{deviceId}")]
    public async Task DeregisterDevice([Required] string deviceId, CancellationToken cancellationToken)
    {
        await pushNotificationService.DeregisterDevice(deviceId, cancellationToken);
    }

#if Development // This action is for testing purposes only.
    [HttpPost]
    public async Task RequestPush([FromQuery] string? title = null, [FromQuery] string? message = null, [FromQuery] string? action = null, CancellationToken cancellationToken = default)
    {
        await pushNotificationService.RequestPush(title, message, action, null, cancellationToken);
    }
#endif
}
