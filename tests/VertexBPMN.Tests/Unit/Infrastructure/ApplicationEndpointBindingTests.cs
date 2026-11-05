using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class ApplicationEndpointBindingTests
{
    [Theory]
    [InlineData("sql", "customers", "home")]
    [InlineData("data-source-read", "foreign-customers", "home")]
    [InlineData("data-source-read", "customers", "foreign-page")]
    public async Task Save_RejectsActionOutsideLocalApprovedCatalog(string type, string target, string page)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var store = new EndpointCheckpointStore();
        await using var db = store.Open();
        await db.Database.MigrateAsync(ct);
        await Assert.ThrowsAsync<ApplicationValidationException>(() => EndpointCheckpointStore.Service(db)
            .CreateAsync("tenant-a", new("binding", "Binding", 0,
                EndpointCheckpointStore.Content(actionType: type, target: target, page: page)), ct));
    }
}
