
namespace Caching.Web.Services;
public interface ICityDataCache
{
    CityData? Get(string zipCode);
    void Set(CityData? data);
}
