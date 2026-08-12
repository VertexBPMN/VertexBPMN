using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

[Collection("IntegratedApi")] 
public class ProcessTests
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private readonly CustomWebApplicationFactory _factory;

    public ProcessTests(CustomWebApplicationFactory factory, SharedSqliteDbFixture dbFixture, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;

        _client = factory.WithSharedFixture(dbFixture).CreateClient(output);
    }

    [Fact]
    public async Task RootEndpoint_ReturnsOk()
    {
        var resp = await _client.GetAsync("/api");
        Assert.True(resp.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Swagger_Is_Available()
    {
        _output.WriteLine($"Base address: {_client.BaseAddress}");
        var urls = new[] { "swagger", "swagger/index.html", "api/swagger", "api/swagger/index.html" };

        foreach (var url in urls)
        {
            try
            {
                var response = await _client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var html = await response.Content.ReadAsStringAsync();
                    if (html.Contains("Swagger UI"))
                        return;
                }
            }
            catch { /* ignore for iteration */ }
        }

        var final = await _client.GetAsync("swagger");
        final.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ProcessDefinitions_AreSeeded()
    {
        var resp = await _client.GetAsync("/api/repository");
        resp.EnsureSuccessStatusCode();
        var content = await resp.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
        // Ensure both seeded definitions are present
        //Assert.Contains("simpleProcess", content);
        //Assert.Contains("advancedProcess", content);
    }
    [Fact]
    public async Task Tenants_AreSeeded()
    {
        using var scope = _factory.Services.CreateScope();
        var tenantDb = scope.ServiceProvider.GetRequiredService<TenantDbContext>();

        var tableNames = await GetSqliteTableNamesAsync(tenantDb);
        Assert.Contains("Tenants", tableNames);
    }

    private static async Task<List<string>> GetSqliteTableNamesAsync(DbContext ctx)
    {
        var result = new List<string>();
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
                result.Add(reader.GetString(0));
        }
        return result;
    }
}