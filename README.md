# Dotnet Caching Example 📜
This repo is designed to aid in understanding basic caching in a .NET application. The branches are set up in a way that you
can analyze the code and practices on the first branch and go through each subsequent branch to see what code was cleaned up
or abstracted away.

## Basic Project Structure / Building 🏗
### Caching.Web
The main blazor UI for the project, contains a simple form for looking up geo metadata of a given zip code.
### Caching.Service
Simple RESTful API with 1 GET endpoint that supplies geo metadata for a supplied zip code, used to simulated executing an actual network call to a remote service.
The data is coming from a local SQLite DB in the project.
### Caching.Shared
Very sparse class lib with a few types to use between both web projects.
### Building and running
- .NET 6.0 SDK is needed, latest available from here [.NET 6 Downloads](https://dotnet.microsoft.com/download/dotnet/6.0)
- You can run the sites from the CLI with just the SDK installed, but to run from Visual Studio you need the following:
	- (Until ~ November 2021) VS 2022-Preview or VS 2019-Preview, it may work in the latest VS 2019 GA version the closer we get to November 2021
## Branches 🌿
The branches go in the following order from least refactored to most refactored and include improvements on the previous branch's caching
(Probably should have numbered the branches, but hindsight is 20/20).

### 1. base-functionality-no-cache 🦨
- This branch has no service layer or cache. All the code is placed in the top level blazor file
```csharp
async Task OnSearch(EditContext context)
{
	Loading = true;
	ErrorMessage = null;

	if(string.IsNullOrWhiteSpace(Model?.SearchText))
	{
		ErrorMessage = "Search is Invalid";
		Loading = false;
		await InvokeAsync(() => StateHasChanged());
		return;
	}

	using var client = ClientFactory.CreateClient();
	client.BaseAddress = new Uri(Configuration.GetValue<string>("ZipCodeServiceUrl"));

	WrappedActionResult<HttpResponseMessage?> result = await RequestStatsWrapper.WrapAsync<HttpResponseMessage>(() => client.GetAsync($"zipcode/{Model.SearchText}"));

	if (!result.HasResult || !(result.Result is HttpResponseMessage response) || !response.IsSuccessStatusCode)
	{
		ErrorMessage = $"Unable to find valid Zip Code for {Model.SearchText}";
		Loading = false;
		await InvokeAsync(() => StateHasChanged());
		return;	
	}

	var options = new JsonSerializerOptions
	{
		AllowTrailingCommas= true,
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	CityData? data = await JsonSerializer.DeserializeAsync<CityData?>(await response.Content.ReadAsStreamAsync(), options);

	if (data is null)
	{
		ErrorMessage = $"Unable to find valid Zip Code for {Model.SearchText}";
		Loading = false;
		await InvokeAsync(() => StateHasChanged());
		return;	
	}

	Loading = false;
	SearchResult = data;
	LastSearchTime = result.Elapsed;
	await InvokeAsync(() => StateHasChanged());
	
}
```


### 2. managed-cache 🛴
- This branch creates a `ZipCodeService` which utilizes `IMemoryCache` directly to cache responses from the REST endpoint.
- The Blazor file now just depends on an `IZipCodeService` instead of everything we need to get the data from the REST service
- This is a good start to caching/logic but still can contain some complexity:
  - The `ZipCodeService is now containing the business rules on how to handle bad data, the logic around caching and the code to get the data from the remote service.
  - Also this can be a pain to write unit tests around because most of the useful methods for the `IMemoryCache` interface are extension methods, which cannot be mocked.
```csharp
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
```

### 3. managed-cache-with-abstraction 🏎
- This branch creates:
  - a `ICityDataCache` which abstracts the caching implementation and configuration away from users of the `ICityDataCache` interface
  - a 'IZipCodeServiceClient` which is a transient service that abstracts the implementation of what technology we are using to go an get the data. It could be HTTP, SQL, gRPC, etc.
- The `ZipCodeService` now can focus more on key business details around the data and not implementation details on how we are getting the data.
- We also made it easier on ourselves to write unit tests around the `ZipCodeService` because we can now easily mock away the behavior of the `ICityDataCache` and `IZipCodeServiceClient`
```csharp
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
            if (_cache.Get(zipCode) is CityData cachedData)
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
```

### 3. cache-polices 🚀
- This branch:
  - Removes the managed `ICityDataCache`
  - Adds an `IAsyncPolicy<CityData?>` to the `IReadOnlyPolicyRegistry` provided by the `Polly.Caching.Memory` NuGet package to be used in the `IZipCodeServiceClient`
  	- This library provides a memory cache that it manages and allows us to wrap any method calls with a caching policy
- The cache is now also abstracted from 'IZipCodeService` which further reduces the plumbing code in the service and allows it to focus on business logic.
- The is by far the easiest scenario to unit test the `ZipCodeService` because we have moved dependant behaviors away from the business logic and we can get right at the logic under test.
```csharp
// DI Registrations
builder.Services.AddSingleton<Polly.Registry.IReadOnlyPolicyRegistry<string>, Polly.Registry.PolicyRegistry>((serviceProvider) =>
{
    PolicyRegistry registry = new();
	
	// Set the cahce policy by name
    registry.Add(
        "zipcodeserviceclient",
        Policy.CacheAsync<CityData>(
            serviceProvider.GetRequiredService<IAsyncCacheProvider>().AsyncFor<CityData>(),
            TimeSpan.FromSeconds(30)
        )
    );

    return registry;
});
```
```csharp
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
	// Get the cahce policy by name
        _cachePolicy = registry.Get<IAsyncPolicy<CityData?>>("zipcodeserviceclient");
    }

    public async Task<CityData?> GetCityDataAsync(string zipCode)
    {
	// Get the data from the remote service if not already in cache
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
```
```csharp
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
```
