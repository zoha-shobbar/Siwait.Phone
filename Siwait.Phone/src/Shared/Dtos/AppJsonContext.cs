using Siwait.Phone.Shared.Dtos.Categories;
using Siwait.Phone.Shared.Dtos.Dashboard;
using Siwait.Phone.Shared.Dtos.Products;
using Siwait.Phone.Shared.Dtos.PushNotification;

namespace Siwait.Phone.Shared.Dtos;

/// <summary>
/// https://devblogs.microsoft.com/dotnet/try-the-new-system-text-json-source-generator/
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(RestErrorInfo))]
[JsonSerializable(typeof(DeviceInstallationDto))]
[JsonSerializable(typeof(List<ProductsCountPerCategoryResponseDto>))]
[JsonSerializable(typeof(OverallAnalyticsStatsDataResponseDto))]
[JsonSerializable(typeof(List<ProductPercentagePerCategoryResponseDto>))]
[JsonSerializable(typeof(ProductDto))]
[JsonSerializable(typeof(PagedResult<ProductDto>))]
[JsonSerializable(typeof(List<ProductDto>))]
[JsonSerializable(typeof(CategoryDto))]
[JsonSerializable(typeof(PagedResult<CategoryDto>))]
[JsonSerializable(typeof(List<CategoryDto>))]
public partial class AppJsonContext : JsonSerializerContext
{
}
