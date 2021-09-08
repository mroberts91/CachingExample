namespace Caching.Web.Services;
public class ZipCodeService : IZipCodeService
{
    private readonly ILogger<ZipCodeService> _logger;
    private readonly ICityDataCache _cache;
    private readonly IZipCodeServiceClient _client;

    public ZipCodeService(ILogger<ZipCodeService> logger, IZipCodeServiceClient client, ICityDataCache cache)
    {
        _logger = logger;
        _client = client;
        _cache = cache;
    }

    public async Task<CityData?> GetZipCodeDataAsync(string zipCode)
    {
        try
        {
            var value = _cache.Get(zipCode);
            if (value is CityData cachedData)
                return cachedData;

            CityData? data = await _client.GetCityDataAsync(zipCode);

            if (data?.ZipCode is null)
                throw new InvalidOperationException($"Unable to find valid Zip Code for {zipCode}");

            _cache.Set(data);

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while attempting to lookup Zip Code: {msg}", ex.Message);
            throw;
        }
    }
}
