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
        var clientMock = new Mock<IZipCodeServiceClient>();
        var cacheMock = new Mock<ICityDataCache>();

        clientMock
            .Setup(o => o.GetCityDataAsync(It.IsAny<string>()))
            .Returns(Task.FromResult<CityData?>(data));

        cacheMock
            .Setup(o => o.Get(It.IsAny<string>()))
            .Returns((CityData?)null);

        // Execute
        var service = new ZipCodeService(Mock.Of<ILogger<ZipCodeService>>(), clientMock.Object, cacheMock.Object);
        var value = await service.GetZipCodeDataAsync("99999");

        // Assert
        value.ShouldBe(data);
    }

    [Fact]
    public async Task GetZipCodeDataAsync_Success_CacheHit()
    {
        // Mock
        var data = new CityDataFaker().Generate();
        var cacheMock = new Mock<ICityDataCache>();

        cacheMock
            .Setup(o => o.Get(It.IsAny<string>()))
            .Returns(data);

        // Execute
        var service = new ZipCodeService(Mock.Of<ILogger<ZipCodeService>>(), Mock.Of<IZipCodeServiceClient>(), cacheMock.Object);
        var value = await service.GetZipCodeDataAsync(It.IsAny<string>());

        // Assert
        value.ShouldBe(data);
    }

    [Fact]
    public async Task GetZipCodeDataAsync_Throw_ResponseNull()
    {
        // Mock
        var data = new CityDataFaker(null).Generate();
        var cacheMock = new Mock<ICityDataCache>();
        var clientMock = new Mock<IZipCodeServiceClient>();

        clientMock
            .Setup(o => o.GetCityDataAsync(It.IsAny<string>()))
            .Returns(Task.FromResult<CityData?>(data));

        cacheMock
            .Setup(o => o.Get(It.IsAny<string>()))
            .Returns((CityData?)null);

        // Execute
        var service = new ZipCodeService(Mock.Of<ILogger<ZipCodeService>>(), Mock.Of<IZipCodeServiceClient>(), cacheMock.Object);

        // Assert
        var zipCode = "99999";
        await service.GetZipCodeDataAsync(zipCode)
            .ShouldThrowAsync<InvalidOperationException>($"Unable to find valid Zip Code for {zipCode}");
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