
namespace Caching.Web.Services;
public class ZipCodeServiceClient : IZipCodeServiceClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ZipCodeServiceClient(HttpClient client)
    {
        _httpClient = client;
    }

    public async Task<CityData?> GetCityDataAsync(string zipCode)
    {
        return await MakeRequestAsync(zipCode);
    }

    private async Task<CityData?> MakeRequestAsync(string zipCode)
    {
        var response = await _httpClient.GetAsync($"zipcode/{zipCode}");
        response.EnsureSuccessStatusCode();

        CityData? data = await JsonSerializer.DeserializeAsync<CityData?>(await response.Content.ReadAsStreamAsync(), _serializerOptions);

        return data;
    }
}
