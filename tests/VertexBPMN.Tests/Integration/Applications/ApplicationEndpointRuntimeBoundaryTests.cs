using System.Net;

namespace VertexBPMN.Tests.Integration.Applications;

public sealed class ApplicationEndpointRuntimeBoundaryTests
{
    [Theory]
    [InlineData("foreign/Invoice")]
    [InlineData("search/Invoice/extra")]
    [InlineData("search")]
    public async Task Get_RejectsUnboundRoute(string route)
    {
        await using var fixture = new EndpointRuntimeFixture();
        await fixture.OpenAsync(EndpointRuntimeContent.Json);
        using var response = await fixture.Http.GetAsync($"/api/apps/{fixture.Key}/endpoints/{route}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Fact]
    public async Task Get_RejectsUnpublishedApplication()
    {
        await using var fixture = new EndpointRuntimeFixture();
        await fixture.OpenAsync(EndpointRuntimeContent.Json, publish: false);
        using var response = await fixture.Http.GetAsync($"/api/apps/{fixture.Key}/endpoints/search/Invoice", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Theory]
    [InlineData("foreign-query")]
    [InlineData("https://foreign.example")]
    public async Task Get_RejectsUnapprovedQuery(string query)
    {
        await using var fixture = new EndpointRuntimeFixture();
        await fixture.OpenAsync(EndpointRuntimeContent.Json.Replace("personal-task-inbox", query, StringComparison.Ordinal));
        using var response = await fixture.Http.GetAsync($"/api/apps/{fixture.Key}/endpoints/search/Invoice", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Fact]
    public async Task Get_RejectsPostOnlyEndpoint()
    {
        await using var fixture = new EndpointRuntimeFixture();
        await fixture.OpenAsync(EndpointRuntimeContent.Json.Replace("\"method\":\"GET\"", "\"method\":\"POST\"", StringComparison.Ordinal));
        using var response = await fixture.Http.GetAsync($"/api/apps/{fixture.Key}/endpoints/search/Invoice", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Fact]
    public async Task Get_RejectsForeignTenant()
    {
        await using var fixture = new EndpointRuntimeFixture();
        await fixture.OpenAsync(EndpointRuntimeContent.Json);
        fixture.Http.DefaultRequestHeaders.Remove("X-Test-Tenant");
        fixture.Http.DefaultRequestHeaders.Add("X-Test-Tenant", "tenant-b");
        using var response = await fixture.Http.GetAsync($"/api/apps/{fixture.Key}/endpoints/search/Invoice", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
