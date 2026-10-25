using System.Net;
using System.Text;
using VertexBPMN.Portal.Client.Bff;

namespace VertexBPMN.Tests.Unit.Portal;

/// <summary>
/// LC06 client BFF service: state-changing calls must carry the antiforgery request token and a
/// fresh Idempotency-Key, and server problem-detail codes must surface as typed results.
/// </summary>
public sealed class PortalBffClientTests
{
    [Fact]
    public async Task Claim_sends_csrf_token_and_idempotency_key()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        using var http = NewHttp(handler);
        var bff = NewClient(http, token: "csrf-123", idemFactory: () => "idem-9");
        var taskId = Guid.NewGuid();

        var result = await bff.ClaimAsync("app-1", taskId, ct);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/bff/applications/app-1/actions/claim/{taskId}", request.RequestUri!.AbsolutePath);
        Assert.Equal("csrf-123", request.Headers.GetValues("X-CSRF-TOKEN").Single());
        Assert.Equal("idem-9", request.Headers.GetValues("Idempotency-Key").Single());
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task StateChanging_actions_all_send_csrf_and_idempotency_headers()
    {
        var ct = TestContext.Current.CancellationToken;
        using var handler = new StubHandler(r =>
            r.RequestUri!.AbsolutePath.EndsWith("/actions/start", StringComparison.Ordinal)
                ? Json(HttpStatusCode.OK, """{"action":"start-process","taskId":null,"status":"accepted"}""")
                : new HttpResponseMessage(HttpStatusCode.NoContent));
        using var http = NewHttp(handler);

        var bff = NewClient(http, token: "t", idemFactory: () => Guid.NewGuid().ToString("N"));

        await bff.StartAsync("app-1", null, ct);
        await bff.CompleteAsync("app-1", Guid.NewGuid(), null, ct);
        await bff.ClaimTaskAsync(Guid.NewGuid(), ct);   // inbox claim
        await bff.CompleteTaskAsync(Guid.NewGuid(), null, ct); // inbox complete

        Assert.Equal(4, handler.Requests.Count);
        Assert.All(handler.Requests, r =>
        {
            Assert.Equal(HttpMethod.Post, r.Method);
            Assert.True(r.Headers.Contains("X-CSRF-TOKEN"), "Missing X-CSRF-TOKEN on " + r.RequestUri);
            Assert.True(r.Headers.Contains("Idempotency-Key"), "Missing Idempotency-Key on " + r.RequestUri);
        });
    }

    [Fact]
    public async Task Antiforgery_token_is_requested_before_state_changing_call()
    {
        var ct = TestContext.Current.CancellationToken;
        var provider = new CountingTokenProvider();
        using var handler = new StubHandler(r =>
            new HttpResponseMessage(r.Method == HttpMethod.Get ? HttpStatusCode.OK : HttpStatusCode.NoContent));
        using var http = NewHttp(handler);
        var bff = new PortalBffClient(http, provider, () => "k");

        await bff.ClaimAsync("app-1", Guid.NewGuid(), ct);

        // The client consulted the token provider before issuing the state-changing call, and the
        // resulting request carried the provider's token. (The provider abstraction is the HTTP seam;
        // its default implementation performs the actual token-fetch GET.)
        Assert.True(provider.GetCalls >= 1);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("t", request.Headers.GetValues("X-CSRF-TOKEN").Single());
    }

    [Fact]
    public async Task Server_problem_code_surfaces_on_unsanctioned_binding()
    {
        var ct = TestContext.Current.CancellationToken;
        const string body = """{"title":"Unsanctioned released binding.","detail":"Data source 'x' is not part of the released application.","status":404,"code":"bffUnsanctionedBinding"}""";
        using var handler = new StubHandler(_ => Json(HttpStatusCode.NotFound, body));
        using var http = NewHttp(handler);
        var bff = NewClient(http, token: "t");

        var result = await bff.ReadDataSourceAsync("app-1", "x", ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("bffUnsanctionedBinding", result.ErrorCode, StringComparer.Ordinal);
        Assert.Equal(404, result.StatusCode);
        Assert.NotNull(result.Detail);
    }

    [Fact]
    public async Task Missing_idempotency_key_code_surfaces_on_complete()
    {
        var ct = TestContext.Current.CancellationToken;
        const string body = """{"title":"Complete.","detail":"An Idempotency-Key header is required.","status":400,"code":"bffMissingIdempotencyKey"}""";
        using var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest, body));
        using var http = NewHttp(handler);
        var bff = NewClient(http, token: "t");

        var result = await bff.CompleteAsync("app-1", Guid.NewGuid(), null, ct);

        Assert.False(result.IsSuccess);
        Assert.Equal("bffMissingIdempotencyKey", result.ErrorCode, StringComparer.Ordinal);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task GetReleased_parses_released_metadata()
    {
        var ct = TestContext.Current.CancellationToken;
        const string body = """{"applicationId":"app-1","version":2,"pageCount":2,"pageRoutes":["/home","/settings"]}""";
        using var handler = new StubHandler(_ => Json(HttpStatusCode.OK, body));
        using var http = NewHttp(handler);
        var bff = NewClient(http, token: "t");

        var result = await bff.GetReleasedAsync("app-1", ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("app-1", result.Value!.ApplicationId, StringComparer.Ordinal);
        Assert.Equal(2, result.Value.Version);
        Assert.Equal(2, result.Value.PageRoutes.Count);
    }

    [Fact]
    public async Task GetTaskStatus_parses_projected_status_view()
    {
        var ct = TestContext.Current.CancellationToken;
        const string body = """{"taskId":"11111111-2222-3333-4444-555555555555","status":"active","candidateRole":"FinanceAgent"}""";
        using var handler = new StubHandler(_ => Json(HttpStatusCode.OK, body));
        using var http = NewHttp(handler);
        var bff = NewClient(http, token: "t");

        var result = await bff.GetTaskStatusAsync("app-1", Guid.Parse("11111111-2222-3333-4444-555555555555"), ct);

        Assert.True(result.IsSuccess);
        Assert.Equal("active", result.Value!.Status, StringComparer.Ordinal);
        Assert.Equal("FinanceAgent", result.Value.CandidateRole, StringComparer.Ordinal);
    }

    [Fact]
    public async Task GetInbox_parses_task_inbox_page()
    {
        var ct = TestContext.Current.CancellationToken;
        const string body = """{"items":[{"id":"11111111-2222-3333-4444-555555555555","name":"Review invoice","status":"pending","processInstanceId":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"}],"total":1,"page":1,"pageSize":20,"hasMore":false}""";
        using var handler = new StubHandler(_ => Json(HttpStatusCode.OK, body));
        using var http = NewHttp(handler);
        var bff = NewClient(http, token: "t");

        var result = await bff.GetInboxAsync(1, 20, ct);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("Review invoice", item.Name, StringComparer.Ordinal);
        Assert.Equal("pending", item.Status, StringComparer.Ordinal);
    }

    private static HttpClient NewHttp(StubHandler handler)
        => new(handler) { BaseAddress = new Uri("https://bff.test/") };

    private static PortalBffClient NewClient(HttpClient http, string token, Func<string>? idemFactory = null)
        => new(http, new StubTokenProvider(token), idemFactory);

    private static HttpResponseMessage Json(HttpStatusCode status, string json)
        => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private sealed class StubTokenProvider(string token) : IBffAntiforgeryTokenProvider
    {
        public Task<string?> GetTokenAsync(CancellationToken ct = default) => Task.FromResult<string?>(token);
    }

    private sealed class CountingTokenProvider : IBffAntiforgeryTokenProvider
    {
        public int GetCalls { get; private set; }
        public Task<string?> GetTokenAsync(CancellationToken ct = default) { GetCalls++; return Task.FromResult<string?>("t"); }
    }
}
