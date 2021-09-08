using Caching.Shared;
using Xunit;
using AutoBogus;
using Moq;
using Caching.Web.Services;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Caching.Web.Tests;

public class ZipCodeServiceTests
{
    [Fact]
    public async Task GetZipCodeDataAsync_Success()
    {
        // Mock
        var data = new CityDataFaker().Generate();
        var clientMock = MockServiceClient(data);

        // Execute
        var service = new ZipCodeService(Mock.Of<ILogger<ZipCodeService>>(), clientMock.Object);
        var value = await service.GetZipCodeDataAsync(It.IsAny<string>());

        // Assert
        value.ShouldBe(data);
    }

    [Fact]
    public async Task GetZipCodeDataAsync_Throw()
    {
        // Mock
        var data = new CityDataFaker(null).Generate();
        var clientMock = MockServiceClient(data);

        // Execute and Assert
        var service = new ZipCodeService(Mock.Of<ILogger<ZipCodeService>>(), clientMock.Object);
        var zipCode = "99999";
        await service.GetZipCodeDataAsync(zipCode)
            .ShouldThrowAsync<InvalidOperationException>($"Unable to find valid Zip Code for {zipCode}");
    }

    private Mock<IZipCodeServiceClient> MockServiceClient(CityData? data)
    {
        var clientMock = new Mock<IZipCodeServiceClient>();
        clientMock
            .Setup(o => o.GetCityDataAsync(It.IsAny<string>()))
            .Returns(Task.FromResult<CityData?>(data));

        return clientMock;
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