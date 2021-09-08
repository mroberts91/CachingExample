
using Polly;
using Polly.Registry;

namespace Caching.Web.Services;
public class ZipCodeServiceClient : IZipCodeServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IAsyncPolicy<CityData?> _cachePolicy;
    private static string CacheKey(string zipCode) => $"ZipCode::{zipCode}";
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ZipCodeServiceClient(HttpClient client, IReadOnlyPolicyRegistry<string> registry)
    {
        _httpClient = client;
        _cachePolicy = registry.Get<IAsyncPolicy<CityData?>>("zipcodeserviceclient");
    }

    public async Task<CityData?> GetCityDataAsync(string zipCode)
    {
        return await _cachePolicy.ExecuteAsync(context => MakeRequestAsync(zipCode), new Context(CacheKey(zipCode)));
    }

    private async Task<CityData?> MakeRequestAsync(string zipCode)
    {
        var response = await _httpClient.GetAsync($"zipcode/{zipCode}");
        response.EnsureSuccessStatusCode();

        CityData? data = await JsonSerializer.DeserializeAsync<CityData?>(await response.Content.ReadAsStreamAsync(), _serializerOptions);

        return data;
    }
}
