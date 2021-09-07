
using Microsoft.Extensions.Caching.Memory;

namespace Caching.Web.Services;

public interface IZipCodeService
{
    Task<WrappedActionResult<CityData?>> GetZipCodeDataAsync(string zipCode);
}

public class ZipCodeService : IZipCodeService
{
    private const string CacheKeyPrefix = "ZipCode::";
    private static string CacheKey(string zipCode) => $"{CacheKeyPrefix}{zipCode}";

    private static MemoryCacheEntryOptions CacheEntryOptions => new()
    {
        AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromMinutes(5),
        Priority = CacheItemPriority.Normal,
        SlidingExpiration = TimeSpan.FromMinutes(1),
    };

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<ZipCodeService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public ZipCodeService(IConfiguration configuration, ILogger<ZipCodeService> logger, IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    public async Task<WrappedActionResult<CityData?>> GetZipCodeDataAsync(string zipCode)
    {
		return await RequestStatsWrapper.WrapAsync(() => GetAndMeasure(zipCode));
	}

    private async Task<CityData?> GetAndMeasure(string zipCode)
    {
        try
        {
            if (_cache.TryGetValue<CityData>(CacheKey(zipCode), out var cachedData))
                return cachedData;

			using var client = _httpClientFactory.CreateClient();
			client.BaseAddress = new Uri(_configuration.GetValue<string>("ZipCodeServiceUrl"));
            var response = await client.GetAsync($"zipcode/{zipCode}");

            if (!response.IsSuccessStatusCode)
				throw new InvalidOperationException($"Unable to find valid Zip Code for {zipCode}");

			CityData? data = await JsonSerializer.DeserializeAsync<CityData?>(await response.Content.ReadAsStreamAsync(), _serializerOptions);

			if (data?.ZipCode is null)
                throw new InvalidOperationException($"Unable to find valid Zip Code for {zipCode}");

            _cache.Set(CacheKey(zipCode), data, CacheEntryOptions);

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while attempting to lookup Zip Code: {msg}", ex.Message);
            throw;
        }
	}

}
