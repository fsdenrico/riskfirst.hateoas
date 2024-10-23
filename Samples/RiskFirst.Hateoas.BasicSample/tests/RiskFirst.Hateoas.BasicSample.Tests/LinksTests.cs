using System.Net.Http.Headers;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using RiskFirst.Hateoas.BasicSample.Models;
using RiskFirst.Hateoas.Models;

namespace RiskFirst.Hateoas.BasicSample.Tests;

public class BasicTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BasicTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAllValues_Json_ReturnsObjectsWithLinks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var values = await client.GetFromJsonAsync<ItemsLinkContainer<ValueInfo>>("/api/values");

        // Assert
        var items = values?.Items ?? Enumerable.Empty<ValueInfo>();

        Assert.All(items, i => Assert.True(i.Links.Count > 0, "Invalid number of links"));
    }

    [Fact]
    public async Task GetAllValues_Xml_ReturnsObjectsWithLinks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        var responseString = await client.GetStringAsync("/api/values");

        var values = DeserializeXml<ItemsLinkContainer<ValueInfo>>(responseString);

        // Assert
        var items = values?.Items ?? Enumerable.Empty<ValueInfo>();

        Assert.All(items, i => Assert.True(i.Links.Count > 0, "Invalid number of links"));
    }

    [Fact]
    public async Task GetValue_Json_AlternateRoute_ReturnsObjectsWithLinks()
    {
        // Arrange
        var client = _factory.CreateClient();

        var test = await client.GetStringAsync("/api/values/v2/1");

        // Act
        var value = await client.GetFromJsonAsync<ValueInfo>("/api/values/v2/1");

        // Assert
        Assert.True(value?.Links.Count > 0, "Invalid number of links");
    }

    [Fact]
    public async Task GetValue_Xml_AlternateRoute_ReturnsObjectsWithLinks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        var responseString = await client.GetStringAsync("/api/values/v2/1");

        var value = DeserializeXml<ValueInfo>(responseString);

        // Assert
        Assert.True(value?.Links.Count > 0, "Invalid number of links");
    }

    [Fact]
    public async Task GetValue_Json_ReturnsObjectsWithLinks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Value
        var value = await client.GetFromJsonAsync<ValueInfo>("/api/values/1");

        // Assert
        Assert.True(value?.Links.Count > 0, "Invalid number of links");
    }

    [Fact]
    public async Task GetValue_Xml_ReturnsObjectsWithLinks()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        var responseString = await client.GetStringAsync("/api/values/1");

        var value = DeserializeXml<ValueInfo>(responseString);

        // Assert
        Assert.True(value?.Links.Count > 0, "Invalid number of links");
    }

    private static T? DeserializeXml<T>(string xml)
    {
        using var reader = new StringReader(xml);

        var serializer = new XmlSerializer(typeof(T));
        return (T?)serializer.Deserialize(reader);
    }
}