using System.Net;
using VertexBPMN.Sdk;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Sdk;

/// <summary>LC05 end-to-end: the SDK drives the full application lifecycle against the real API.</summary>
public sealed class ApplicationApiTests
{
    private static string Content(params object[] pages) =>
        System.Text.Json.JsonSerializer.Serialize(new { pages });

    [Fact]
    public async Task ApplicationLifecycle_ThroughSdk_CreateUpdateRevisionRelease()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var http = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var sdk = new VertexBpmnClient(http);
        var key = "lc05-app-" + Guid.NewGuid().ToString("N");
        const string tenant = "tenant-a";

        // Create draft (revision 1).
        var draft = await sdk.CreateApplicationAsync(key, "LC05 application", Content(new { id = "home", route = "/home" }), tenant, ct);
        Assert.NotNull(draft);
        Assert.Equal(1, draft.Version);

        // Read fixed revision 1.
        var rev1 = await sdk.GetApplicationRevisionAsync(draft.Id, 1, tenant, ct);
        Assert.NotNull(rev1);
        Assert.Equal(1, rev1.Version);

        // Update draft with matching expectedVersion -> revision 2.
        await sdk.UpdateApplicationAsync(draft.Id, key, "LC05 v2", 1, Content(new { id = "home", route = "/home" }, new { id = "settings", route = "/settings" }), tenant, ct);
        var draftV2 = await sdk.GetApplicationAsync(draft.Id, tenant, ct);
        Assert.NotNull(draftV2);
        Assert.Equal(2, draftV2.Version);

        // Stale concurrency: wrong expectedVersion must be rejected with 409 via the SDK.
        var stale = await Assert.ThrowsAsync<HttpRequestException>(
            () => sdk.UpdateApplicationAsync(draft.Id, key, "stale", 1, Content(new { id = "home", route = "/home" }), tenant, ct));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        // Pinned release candidate from revision 2, then release it.
        var candidate = await sdk.CreateApplicationReleaseCandidateAsync(draft.Id, 2, tenant, ct);
        Assert.NotNull(candidate);
        var fetched = await sdk.GetApplicationReleaseCandidateAsync(candidate.Id, tenant, ct);
        Assert.NotNull(fetched);
        Assert.Equal(candidate.Id, fetched.Id);
        await sdk.ReleaseApplicationAsync(candidate.Id, tenant, ct);

        // Draft a new revision 3 AFTER the release; the released read must NOT leak it.
        await sdk.UpdateApplicationAsync(draft.Id, key, "LC05 v3", 2, Content(new { id = "home", route = "/home" }, new { id = "admin", route = "/admin" }), tenant, ct);
        var draftV3 = await sdk.GetApplicationAsync(draft.Id, tenant, ct);
        Assert.NotNull(draftV3);
        Assert.Equal(3, draftV3.Version);

        var released = await sdk.GetApplicationReleasedRevisionAsync(draft.Id, tenant, ct);
        Assert.NotNull(released);
        Assert.Equal(2, released.Version);
        Assert.Contains("settings", released.ContentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("admin", released.ContentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ForeignTenant_ReadAndRelease_Return403()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var adminHttp = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var admin = new VertexBpmnClient(adminHttp);
        var key = "lc05-ten-" + Guid.NewGuid().ToString("N");
        const string tenant = "tenant-a";

        var draft = await admin.CreateApplicationAsync(key, "LC05", Content(new { id = "home", route = "/home" }), tenant, ct);
        Assert.NotNull(draft);
        var candidate = await admin.CreateApplicationReleaseCandidateAsync(draft.Id, 1, tenant, ct);
        Assert.NotNull(candidate);

        using var readerHttp = factory.CreateClient();
        readerHttp.DefaultRequestHeaders.Add("X-Test-User", "reader");
        readerHttp.DefaultRequestHeaders.Add("X-Test-Tenant", "tenant-b");
        var reader = new VertexBpmnClient(readerHttp);

        var read = await Assert.ThrowsAsync<HttpRequestException>(
            () => reader.GetApplicationAsync(draft.Id, tenant, ct));
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);

        var release = await Assert.ThrowsAsync<HttpRequestException>(
            () => reader.ReleaseApplicationAsync(candidate.Id, tenant, ct));
        Assert.Equal(HttpStatusCode.Forbidden, release.StatusCode);
    }
}
