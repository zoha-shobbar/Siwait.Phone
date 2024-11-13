namespace Siwait.Phone.Client.Core.Services.Contracts;

public interface IAuthTokenProvider
{
    Task<string?> GetAccessToken();
}
