using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence.Services;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

public sealed class TenantApiSecurityTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TenantApiSecurityTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ReadOnlyUser_CanOnlyListAndReadClaimTenant()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
            if (!await database.Tenants.AnyAsync(tenant => tenant.Id == "tenant-a", TestContext.Current.CancellationToken))
                database.Tenants.Add(new Tenant { Id = "tenant-a", Name = "Tenant A" });
            if (!await database.Tenants.AnyAsync(tenant => tenant.Id == "tenant-b", TestContext.Current.CancellationToken))
                database.Tenants.Add(new Tenant { Id = "tenant-b", Name = "Tenant B" });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", "readonly-a");
        client.DefaultRequestHeaders.Add("X-Test-Tenant", "tenant-a");

        var list = await client.GetFromJsonAsync<Tenant[]>("/api/tenant", TestContext.Current.CancellationToken);
        var own = await client.GetAsync("/api/tenant/tenant-a", TestContext.Current.CancellationToken);
        var foreign = await client.GetAsync("/api/tenant/tenant-b", TestContext.Current.CancellationToken);

        var visible = Assert.Single(list!);
        Assert.Equal("tenant-a", visible.Id);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
    }
}
