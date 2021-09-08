
namespace Caching.Web.Services;
public interface IZipCodeService
{
    Task<CityData?> GetZipCodeDataAsync(string zipCode);
}
