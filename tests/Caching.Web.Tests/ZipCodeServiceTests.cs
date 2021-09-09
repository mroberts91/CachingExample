using Caching.Shared;
using Xunit;
using AutoBogus;
using Moq;
using Caching.Web.Services;
using Microsoft.Extensions.Logging;
using Shouldly;
using Moq.Protected;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;

namespace Caching.Web.Tests;

public class ZipCodeServiceTests
{
    [Fact]
    public async Task GetZipCodeDataAsync_Success_CacheMiss()
    {
        // Mock
        var data = new CityDataFaker().Generate();
        var clientMock = new Mock<IHttpClientFactory>();
        var configSection = new Mock<IConfigurationSection>();
        var configMock = new Mock<IConfiguration>();
        var cacheMock = new Mock<IMemoryCache>();

        clientMock
            .Setup(o => o.CreateClient(It.IsAny<string>()))
            .Returns(BuildTestHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(data)));

        configSection
            .Setup(o => o.Value)
            .Returns("https://foo.com/");

        configMock
            .Setup(o => o.GetSection(It.IsAny<string>()))
            .Returns(configSection.Object);

        object? cacheReturn = null;
        cacheMock
            .Setup(o => o.TryGetValue(It.IsAny<string>(), out cacheReturn))
            .Returns(false);

        cacheMock
            .Setup(o => o.CreateEntry(It.IsAny<object>()))
            .Returns(Mock.Of<ICacheEntry>());

        // Execute
        var service = new ZipCodeService(configMock.Object, Mock.Of<ILogger<ZipCodeService>>(), clientMock.Object, cacheMock.Object);
        var value = await service.GetZipCodeDataAsync("99999");

        // Assert
        value.Elapsed.TotalMilliseconds.ShouldBeGreaterThan(0);
        value.HasResult.ShouldBeTrue();
        value.Exception.ShouldBeNull();
        value.ExceptionMessage.ShouldBeNull();
        value.Result.ShouldBe(data);
    }

    [Fact]
    public async Task GetZipCodeDataAsync_Success_CacheHit()
    {
        // Mock
        var data = new CityDataFaker().Generate();
        var cacheMock = new Mock<IMemoryCache>();

        var outData = (object)data;
        cacheMock
            .Setup(o => o.TryGetValue(It.IsAny<string>(), out outData))
            .Returns(true);

        cacheMock
            .Setup(o => o.CreateEntry(It.IsAny<object>()))
            .Returns(Mock.Of<ICacheEntry>());

        // Execute
        var service = new ZipCodeService(Mock.Of<IConfiguration>(), Mock.Of<ILogger<ZipCodeService>>(), Mock.Of<IHttpClientFactory>(), cacheMock.Object);
        var value = await service.GetZipCodeDataAsync("99999");

        // Assert
        value.Elapsed.TotalMilliseconds.ShouldBeGreaterThan(0);
        value.HasResult.ShouldBeTrue();
        value.Exception.ShouldBeNull();
        value.ExceptionMessage.ShouldBeNull();
        value.Result.ShouldBe(data);
    }

    [Fact]
    public async Task GetZipCodeDataAsync_Throw_HttpFailure()
    {
        // Mock
        var data = new CityDataFaker().Generate();
        var clientMock = new Mock<IHttpClientFactory>();
        var configSection = new Mock<IConfigurationSection>();
        var configMock = new Mock<IConfiguration>();
        var cacheMock = new Mock<IMemoryCache>();

        clientMock
            .Setup(o => o.CreateClient(It.IsAny<string>()))
            .Returns(BuildTestHttpClient(HttpStatusCode.BadRequest));

        configSection
            .Setup(o => o.Value)
            .Returns("https://foo.com/");

        configMock
            .Setup(o => o.GetSection(It.IsAny<string>()))
            .Returns(configSection.Object);

        object? cacheReturn = null;
        cacheMock
            .Setup(o => o.TryGetValue(It.IsAny<string>(), out cacheReturn))
            .Returns(false);

        cacheMock
            .Setup(o => o.CreateEntry(It.IsAny<object>()))
            .Returns(Mock.Of<ICacheEntry>());

        // Execute
        var service = new ZipCodeService(configMock.Object, Mock.Of<ILogger<ZipCodeService>>(), clientMock.Object, cacheMock.Object);
        var zipCode = "99999";
        var result = await service.GetZipCodeDataAsync(zipCode);

        // Assert
        result.Exception.ShouldBeOfType<InvalidOperationException>();
        result.ExceptionMessage.ShouldBe($"Unable to find valid Zip Code for {zipCode}");
        result.HasResult.ShouldBeFalse();
    }

    [Fact]
    public async Task GetZipCodeDataAsync_Throw_ResponseNull()
    {
        // Mock
        var data = new CityDataFaker(null).Generate();
        var clientMock = new Mock<IHttpClientFactory>();
        var configSection = new Mock<IConfigurationSection>();
        var configMock = new Mock<IConfiguration>();
        var cacheMock = new Mock<IMemoryCache>();

        clientMock
            .Setup(o => o.CreateClient(It.IsAny<string>()))
            .Returns(BuildTestHttpClient(HttpStatusCode.OK, JsonSerializer.Serialize(data)));

        configSection
            .Setup(o => o.Value)
            .Returns("https://foo.com/");

        configMock
            .Setup(o => o.GetSection(It.IsAny<string>()))
            .Returns(configSection.Object);

        object? cacheReturn = null;
        cacheMock
            .Setup(o => o.TryGetValue(It.IsAny<string>(), out cacheReturn))
            .Returns(false);

        cacheMock
            .Setup(o => o.CreateEntry(It.IsAny<object>()))
            .Returns(Mock.Of<ICacheEntry>());

        // Execute
        var service = new ZipCodeService(configMock.Object, Mock.Of<ILogger<ZipCodeService>>(), clientMock.Object, cacheMock.Object);
        var zipCode = "99999";
        var result = await service.GetZipCodeDataAsync(zipCode);

        // Assert
        result.Exception.ShouldBeOfType<InvalidOperationException>();
        result.ExceptionMessage.ShouldBe($"Unable to find valid Zip Code for {zipCode}");
        result.HasResult.ShouldBeFalse();
    }

    private HttpClient BuildTestHttpClient(HttpStatusCode responseStatus, string responseJson = "")
    {
        var HttpClientMockHandler = new Mock<HttpMessageHandler>();
        HttpClientMockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
           .ReturnsAsync(new HttpResponseMessage()
           {
               StatusCode = responseStatus,
               Content = new StringContent(responseJson),
           })
           .Verifiable();

        return new HttpClient(HttpClientMockHandler.Object);
    }
}

public class CityDataFaker : AutoFaker<CityData>
{
    public CityDataFaker()
    {
        RuleFor(fake => fake.ZipCode, fake => fake.Address.ZipCode());
        SetMetadataRules();
    }

    public CityDataFaker(string? zipCode)
    {
        RuleFor(fake => fake.ZipCode, zipCode);
        SetMetadataRules();
    }

    private void SetMetadataRules()
    {
        RuleFor(fake => fake.City, fake => fake.Address.City());
        RuleFor(fake => fake.State, fake => fake.Address.State());
        RuleFor(fake => fake.StateAbbreviation, fake => fake.Address.StateAbbr());
        RuleFor(fake => fake.County, fake => fake.Address.County());
        RuleFor(fake => fake.Longitude, fake => (float)fake.Address.Longitude());
        RuleFor(fake => fake.Latitude, fake => (float)fake.Address.Latitude());
    }
}