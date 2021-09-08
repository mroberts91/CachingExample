
using Microsoft.Extensions.Caching.Memory;

namespace Caching.Web.Services;
public class CityDataCache : ICityDataCache
{
    private const string CacheKeyPrefix = "CityData::";
    private static string CacheKey(string zipCode) => $"{CacheKeyPrefix}{zipCode}";

    private static MemoryCacheEntryOptions CacheEntryOptions => new()
    {
        AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromMinutes(5),
        Priority = CacheItemPriority.Normal,
        SlidingExpiration = TimeSpan.FromMinutes(1),
    };

    private readonly IMemoryCache _cache;

    public CityDataCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public CityData? Get(string zipCode)
    {
        if (string.IsNullOrWhiteSpace(zipCode))
            return default;

        return 
            _cache.TryGetValue<CityData>(CacheKey(zipCode), out var data)
            ? data 
            : default;
    }

    public void Set(CityData? data)
    {
        if (data is not CityData { ZipCode: string } cityData )
            return;

        _cache.Set(CacheKey(cityData.ZipCode), cityData);
    }


}
