namespace Caching.Web.Services;
public class ZipCodeService : IZipCodeService
{
    private readonly ILogger<ZipCodeService> _logger;
    private readonly IZipCodeServiceClient _client;

    public ZipCodeService(ILogger<ZipCodeService> logger, IZipCodeServiceClient client)
    {
        _logger = logger;
        _client = client;
    }

    public async Task<CityData?> GetZipCodeDataAsync(string zipCode)
    {
        try
        {
            CityData? data = await _client.GetCityDataAsync(zipCode);

            if (data?.ZipCode is null)
                throw new InvalidOperationException($"Unable to find valid Zip Code for {zipCode}");

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while attempting to lookup Zip Code: {msg}", ex.Message);
            throw;
        }
    }
}
