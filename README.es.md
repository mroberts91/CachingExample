# Ejemplo de almacenamiento en caché de Dotnet 📜

Este repositorio está diseñado para ayudar a comprender el almacenamiento en caché básico en una aplicación .NET. Las ramas están configuradas de manera que usted
puede analizar el código y las prácticas en la primera rama y pasar por cada rama subsiguiente para ver qué código se limpió
o abstraído.

## Estructura / Edificio Básico del Proyecto 🏗

### Caching.Web

La interfaz de usuario principal de blazor para el proyecto contiene un formulario simple para buscar metadatos geográficos de un código postal determinado.

### Servicio de almacenamiento en caché

API RESTful simple con 1 punto final GET que proporciona metadatos geográficos para un código postal proporcionado, que se utiliza para simular la ejecución de una llamada de red real a un servicio remoto.
Los datos provienen de una base de datos SQLite local en el proyecto.

### Almacenamiento en caché Compartido

Lib de clase muy escasa con algunos tipos para usar entre ambos proyectos web.

### Construyendo y funcionando

-   Se necesita .NET 6.0 SDK, el último disponible desde aquí[.NET 6 Descargas](https://dotnet.microsoft.com/download/dotnet/6.0)
-   Puede ejecutar los sitios desde la CLI con solo el SDK instalado, pero para ejecutar desde Visual Studio necesita lo siguiente:
    -   (Hasta ~ noviembre de 2021) VS 2022-Preview o VS 2019-Preview, puede funcionar en la última versión de VS 2019 GA cuanto más nos acerquemos a noviembre de 2021

## Ramas 🌿

Las ramas van en el siguiente orden desde la menos refactorizada a la más refactorizada e incluyen mejoras en el almacenamiento en caché de la rama anterior.
(Probablemente debería haber numerado las ramas, pero en retrospectiva es 20/20).

### 1. funcionalidad básica sin caché 🦨

-   Esta rama no tiene capa de servicio ni caché. Todo el código se coloca en el archivo blazor de nivel superior

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

### 2. caché administrado 🛴

-   Esta rama crea una`ZipCodeService`que utiliza`IMemoryCache`directamente para almacenar en caché las respuestas del punto final REST.
-   El archivo Blazor ahora solo depende de un`IZipCodeService`en lugar de todo lo que necesitamos para obtener los datos del servicio REST
-   Este es un buen comienzo para el almacenamiento en caché / lógica, pero aún puede contener cierta complejidad:
    -   El \`ZipCodeService ahora contiene las reglas comerciales sobre cómo manejar los datos incorrectos, la lógica en torno al almacenamiento en caché y el código para obtener los datos del servicio remoto.
    -   Además, escribir pruebas unitarias puede resultar complicado porque la mayoría de los métodos útiles para`IMemoryCache`interfaz son métodos de extensión, que no se pueden burlar.

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

### 3. caché administrado con abstracción 🏎

-   Esta rama crea:
    -   a`ICityDataCache`que abstrae la implementación y configuración del almacenamiento en caché lejos de los usuarios del`ICityDataCache`interfaz
    -   un 'IZipCodeServiceClient' que es un servicio transitorio que abstrae la implementación de la tecnología que estamos usando para ir y obtener los datos. Podría ser HTTP, SQL, gRPC, etc.
-   los`ZipCodeService`ahora puede centrarse más en los detalles comerciales clave en torno a los datos y no en los detalles de implementación sobre cómo obtenemos los datos.
-   También nos facilitamos la tarea de escribir pruebas unitarias en torno a`ZipCodeService`porque ahora podemos burlarnos fácilmente del comportamiento del`ICityDataCache`y`IZipCodeServiceClient`

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

### 4. cubierta de fuente 🚀

-   Esta rama:
    -   Elimina el gestionado`ICityDataCache`
    -   Agrega un`IAsyncPolicy<CityData?>`al`IReadOnlyPolicyRegistry`proporcionado por el`Polly.Caching.Memory`Paquete NuGet que se utilizará en el`IZipCodeServiceClient`
        -   Esta biblioteca proporciona una memoria caché que administra y nos permite envolver cualquier llamada de método con una política de almacenamiento en caché.
-   El caché ahora también se extrae de 'IZipCodeService', lo que reduce aún más el código de plomería en el servicio y le permite centrarse en la lógica empresarial.
-   El es, con mucho, el escenario más fácil para probar unitariamente el`ZipCodeService`porque hemos alejado los comportamientos dependientes de la lógica empresarial y podemos llegar directamente a la lógica bajo prueba.

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
