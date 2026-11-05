using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Tests.Integration.Applications;

public sealed class ApplicationEndpointRuntimeTests
{
    [Fact]
    public async Task Get_RejectsParameterOutsideApprovedQueryContract()
    {
        await using var fixture = new EndpointRuntimeFixture();
        await fixture.OpenAsync(EndpointRuntimeContent.Json.Replace("{search}", "{other}", StringComparison.Ordinal)
            .Replace("[\"search\"]", "[\"other\"]", StringComparison.Ordinal)
            .Replace("\"search\":{\"type\":\"string\"", "\"other\":{\"type\":\"string\"", StringComparison.Ordinal));
        var failure = await Record.ExceptionAsync(async () =>
        {
            using var response = await fixture.Http.GetAsync($"/api/apps/{fixture.Key}/endpoints/search/Invoice", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        });
        Assert.Null(failure);
    }

    [Fact]
    public async Task Get_MapsNamedParameterRatherThanWholeRoute()
    {
        await using var fixture = new EndpointRuntimeFixture();
        await fixture.OpenAsync(EndpointRuntimeContent.Json.Replace("/endpoints/search/{search}", "/endpoints/search/by-name/{search}", StringComparison.Ordinal));
        using var response = await fixture.Http.GetAsync($"/api/apps/{fixture.Key}/endpoints/search/by-name/Invoice", TestContext.Current.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Single(body.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Get_RejectsUnsupportedDataSourceVersion()
    {
        await using var fixture = new EndpointRuntimeFixture();
        await fixture.OpenAsync(EndpointRuntimeContent.Json.Replace("\"bindingVersion\":2", "\"bindingVersion\":999", StringComparison.Ordinal));
        using var response = await fixture.Http.GetAsync($"/api/apps/{fixture.Key}/endpoints/search/Invoice", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_EnforcesEndpointPermission()
    {
        await using var fixture = new EndpointRuntimeFixture();
        await fixture.OpenAsync(EndpointRuntimeContent.Json.Replace("\"permission\":\"ReadOnly\"", "\"permission\":\"ProcessManager\"", StringComparison.Ordinal));
        using var response = await fixture.Http.GetAsync($"/api/apps/{fixture.Key}/endpoints/search/Invoice", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_RejectsResponseOutsideOutputSchema()
    {
        await using var fixture = new EndpointRuntimeFixture();
        await fixture.OpenAsync(EndpointRuntimeContent.Json.Replace("\"id\":{\"type\":\"string\"}", "\"id\":{\"type\":\"integer\"}", StringComparison.Ordinal));
        using var response = await fixture.Http.GetAsync($"/api/apps/{fixture.Key}/endpoints/search/Invoice", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Get_RejectsOversizedPathParameter()
    {
        await using var fixture = new EndpointRuntimeFixture();
        await fixture.OpenAsync(EndpointRuntimeContent.Json);
        using var response = await fixture.Http.GetAsync($"/api/apps/{fixture.Key}/endpoints/search/{new string('x', 33)}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsRealRowsFilteredByPathParameter()
    {
        await using var fixture = new EndpointRuntimeFixture();
        await fixture.OpenAsync(EndpointRuntimeContent.Json);
        using var response = await fixture.Http.GetAsync($"/api/apps/{fixture.Key}/endpoints/search/Invoice", TestContext.Current.CancellationToken);
        var body = response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken) : default;
        Assert.Equal("Invoice Alpha", body.ValueKind == JsonValueKind.Object
            ? body.GetProperty("items")[0].GetProperty("name").GetString() : response.StatusCode.ToString());
    }
}
