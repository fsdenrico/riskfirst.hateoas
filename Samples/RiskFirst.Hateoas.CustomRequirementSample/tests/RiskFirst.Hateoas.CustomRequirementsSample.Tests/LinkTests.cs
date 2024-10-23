using Microsoft.AspNetCore.Mvc.Testing;
using RiskFirst.Hateoas.CustomRequirementSample.Models;
using RiskFirst.Hateoas.CustomRequirementsSample.Tests;
using RiskFirst.Hateoas.Models;
using System.Net.Http.Json;

namespace RiskFirst.Hateoas.CustomRequirementsSample.TestsNew;

public class RootApiTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public RootApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAllValues_Json_ReturnsLinksWithRootApi()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var test = await client.GetStringAsync("/api/values");

        var values = await client.GetFromJsonAsync<ItemsLinkContainer<ValueInfo>>("/api/values");

        Assert.Contains(
            new Link
            {
                Href = "http://localhost/api",
                Method = "GET",
                Name = "root",
                Rel = "Root/ApiRoot"
            },
            values.Links,
            new LinkEqualityComparer());
    }
}
