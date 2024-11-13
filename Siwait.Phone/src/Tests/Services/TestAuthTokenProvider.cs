using Siwait.Phone.Client.Core.Services.Contracts;

namespace Siwait.Phone.Tests.Services;

public partial class TestAuthTokenProvider : IAuthTokenProvider
{
    [AutoInject] private IStorageService storageService = default!;

    public async Task<string?> GetAccessToken()
    {
        return await storageService.GetItem("access_token");
    }
}
