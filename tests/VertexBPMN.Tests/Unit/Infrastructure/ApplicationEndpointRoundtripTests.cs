using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class ApplicationEndpointRoundtripTests
{
    [Fact]
    public async Task SavedRevision_ReopenedStoreReadsTypedEndpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = new EndpointCheckpointStore();
        string id;
        await using (var db = store.Open())
        {
            await db.Database.MigrateAsync(ct);
            var saved = await EndpointCheckpointStore.Service(db).CreateAsync("tenant-a",
                new("endpoint-app", "Endpoint", 0, EndpointCheckpointStore.Content()), ct);
            id = saved.Id;
        }
        await using var reopened = store.Open();
        var revision = await EndpointCheckpointStore.Service(reopened).GetRevisionAsync("tenant-a", id, 1, ct);
        var endpoints = ApplicationEndpointMetadata.Read(revision!.ContentJson);
        var endpoint = Assert.Single(endpoints);
        Assert.Equal("customers", endpoint.Key);
        Assert.Equal("GET", endpoint.Method);
        Assert.Equal("/endpoints/customers/{customerId}", endpoint.Route);
        Assert.Equal(["customerId"], endpoint.PathParameters);
        Assert.Equal("object", endpoint.InputSchema.GetProperty("type").GetString());
        Assert.Equal("object", endpoint.OutputSchema.GetProperty("type").GetString());
        Assert.Equal("ReadOnly", endpoint.Permission);
        Assert.Equal(new ApplicationEndpointLimits(5, 1024, 4096), endpoint.Limits);
        Assert.Equal(new ApplicationEndpointActionBinding("home", "data-source-read", "1.0.0", "customers"), endpoint.Action);
        Assert.Equal(EndpointCheckpointStore.Content(), revision.ContentJson);
        Assert.Null(await EndpointCheckpointStore.Service(reopened).GetRevisionAsync("tenant-b", id, 1, ct));
    }
}
