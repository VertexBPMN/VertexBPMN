using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class ApplicationEndpointRouteTests
{
    [Theory]
    [InlineData("TRACE", "/endpoints/customers/{customerId}")]
    [InlineData("GET", "/api/runtime/start")]
    [InlineData("GET", "https://foreign.example/customers")]
    [InlineData("GET", "/endpoints/../runtime/{customerId}")]
    [InlineData("GET", "/endpoints/customers/{otherId}")]
    public async Task Save_RejectsUnapprovedMethodOrRoute(string method, string route)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = new EndpointCheckpointStore();
        await using var db = store.Open();
        await db.Database.MigrateAsync(ct);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => EndpointCheckpointStore.Service(db)
            .CreateAsync("tenant-a", new("route", "Route", 0,
                EndpointCheckpointStore.Content(method: method, route: route)), ct));
    }
}
