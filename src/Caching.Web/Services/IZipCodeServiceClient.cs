
namespace Caching.Web.Services;
public interface IZipCodeServiceClient
{
    Task<CityData?> GetCityDataAsync(string zipCode);
}
