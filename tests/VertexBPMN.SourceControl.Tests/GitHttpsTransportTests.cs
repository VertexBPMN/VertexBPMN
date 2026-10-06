using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed class GitHttpsTransportTests
{
    [Fact]
    public async Task Real_TLS_challenge_helper_and_fetch_preserve_exact_bytes_without_secret_artifacts()
    {
        await using var fixture = await HttpsFixture.CreateAsync();
        using var token = new GitHubTokenLease(fixture.Token, DateTimeOffset.UtcNow.AddMinutes(2));
        await fixture.Runner.FetchLocalAcceptanceAsync(fixture.Workspace, fixture.Remote, "master", token, fixture.CaFile, TestContext.Current.CancellationToken);
        Assert.True(fixture.Challenges > 0);
        Assert.True(fixture.AuthenticatedRequests > 0);
        var content = await fixture.GitAsync(["--git-dir=" + Path.Combine(fixture.Workspace.Directory, "repository.git"),
            "show", "refs/heads/vertex-source:models/example.bpmn"]);
        Assert.Equal(fixture.ModelBytes, content);
        foreach (var path in Directory.EnumerateFiles(fixture.Workspace.Directory, "*", SearchOption.AllDirectories))
            Assert.DoesNotContain(fixture.Token, Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken)));
        Assert.False(File.Exists(Path.Combine(fixture.Workspace.Directory, "repository.git", "hooks", "post-checkout")));
        Assert.False(File.Exists(Path.Combine(fixture.Workspace.Directory, "models", "example.bpmn"))); // Bare, no filters/checkout.
    }

    [Fact]
    public async Task Untrusted_certificate_is_rejected_before_any_authentication()
    {
        await using var fixture = await HttpsFixture.CreateAsync();
        using var other = RSA.Create(2048);
        var request = new CertificateRequest("CN=unrelated", other, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var wrongCa = Path.Combine(fixture.Workspace.ControlDirectory, "wrong-ca.pem");
        await File.WriteAllTextAsync(wrongCa, certificate.ExportCertificatePem(), TestContext.Current.CancellationToken);
        using var token = new GitHubTokenLease(fixture.Token, DateTimeOffset.UtcNow.AddMinutes(2));
        var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fixture.Runner.FetchLocalAcceptanceAsync(fixture.Workspace,
            fixture.Remote, "master", token, wrongCa, TestContext.Current.CancellationToken));
        Assert.Equal(SourceControlErrorCode.ProviderUnavailable, error.Code);
        Assert.Equal(0, fixture.Challenges);
        Assert.Equal(0, fixture.AuthenticatedRequests);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Running_fetch_timeout_or_cancellation_closes_authenticated_child_connection(bool timeout)
    {
        await using var fixture = await HttpsFixture.CreateAsync(stall: true);
        using var token = new GitHubTokenLease(fixture.Token, DateTimeOffset.UtcNow.AddMinutes(2));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var fetch = fixture.Runner.FetchLocalAcceptanceAsync(fixture.Workspace, fixture.Remote, "master", token,
            fixture.CaFile, cancellation.Token);
        await fixture.RequestStalled.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        Assert.True(fixture.AuthenticatedRequests > 0); // Real Git/helper already reached the server.
        if (timeout)
        {
            var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => fetch);
            Assert.Equal(SourceControlErrorCode.TimedOut, error.Code);
        }
        else
        {
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetch);
        }
        // Git's HTTPS subprocess owns the socket. A surviving child would leave this request open.
        await fixture.RequestClosed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Hostile_hooks_filters_and_submodules_are_not_executed_by_protected_fetch()
    {
        await using var fixture = await HttpsFixture.CreateAsync(hostile: true);
        using var token = new GitHubTokenLease(fixture.Token, DateTimeOffset.UtcNow.AddMinutes(2));
        await fixture.Runner.FetchLocalAcceptanceAsync(fixture.Workspace, fixture.Remote, "master", token,
            fixture.CaFile, TestContext.Current.CancellationToken);
        Assert.False(File.Exists(fixture.ExecutionMarker));
        Assert.False(Directory.Exists(Path.Combine(fixture.Workspace.Directory, "modules")));
        var repository = "--git-dir=" + Path.Combine(fixture.Workspace.Directory, "repository.git");
        var tree = Encoding.UTF8.GetString(await fixture.GitAsync([repository, "ls-tree", "refs/heads/vertex-source:modules"]));
        Assert.Contains("160000 commit", tree); // Actual gitlink, not only a .gitmodules placeholder.
        var commit = Encoding.UTF8.GetString(await fixture.GitAsync([repository, "rev-parse", "refs/heads/vertex-source"])).Trim();
        // Independent unsafe control proves that the planted hook is executable.
        // Its only effect is writing this fixture's marker; never a user/system path.
        await fixture.GitAsync([repository, "update-ref", "refs/heads/unsafe-control", commit]);
        Assert.True(File.Exists(fixture.ExecutionMarker));
    }

    [Fact]
    public async Task Commit_objects_preserve_exact_bytes_other_files_and_are_deterministic_without_checkout()
    {
        await using var fixture = await HttpsFixture.CreateAsync(hostile: true);
        using var lease = new GitHubTokenLease(fixture.Token, DateTimeOffset.UtcNow.AddMinutes(2));
        await fixture.Runner.FetchLocalAcceptanceAsync(fixture.Workspace, fixture.Remote, "master", lease,
            fixture.CaFile, TestContext.Current.CancellationToken);
        var repository = "--git-dir=" + Path.Combine(fixture.Workspace.Directory, "repository.git");
        var basis = new GitCommitId(Encoding.UTF8.GetString(await fixture.GitAsync([repository, "rev-parse", "refs/heads/vertex-source"])).Trim());
        var session = Guid.NewGuid();
        var bytes = Encoding.UTF8.GetBytes("<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\">\r\n<process id=\"updated\"/>\r\n</definitions>\r\n");
        var binding = new RepositoryBinding(Guid.NewGuid(), "test", fixture.Remote, null, "master", "release", ["models"]);
        var command = new CommitCommand(Guid.NewGuid(), new SourceControlIdempotencyKey("commit-test"), session,
            basis, SourceControlInputPolicy.WorkBranch(session), "Exact snapshot", [new ModelSnapshot("models/example.bpmn", SourceModelKind.Bpmn, Guid.NewGuid(), 7, bytes)]);
        var accepted = DateTimeOffset.FromUnixTimeSeconds(1800000000);
        var first = await fixture.Runner.BuildCommitAsync(fixture.Workspace, binding, command, accepted, TestContext.Current.CancellationToken);
        var second = await fixture.Runner.BuildCommitAsync(fixture.Workspace, binding, command, accepted, TestContext.Current.CancellationToken);
        Assert.Equal(first, second);
        Assert.Equal(bytes, await fixture.GitAsync([repository, "show", first.Value + ":models/example.bpmn"]));
        Assert.Equal(basis.Value, Encoding.UTF8.GetString(await fixture.GitAsync([repository, "rev-parse", first.Value + "^"])).Trim());
        foreach (var path in new[] { ".gitattributes", ".gitmodules", "modules" })
            Assert.Equal(await fixture.GitAsync([repository, "ls-tree", basis.Value, "--", path]),
                await fixture.GitAsync([repository, "ls-tree", first.Value, "--", path]));
        Assert.False(File.Exists(fixture.ExecutionMarker));
        Assert.Empty(Directory.GetFiles(fixture.Workspace.ControlDirectory, "commit-index-*"));
        var unsafeSnapshot = new ModelSnapshot("Models/example.bpmn", SourceModelKind.Bpmn, Guid.NewGuid(), 1, bytes);
        Assert.Equal(SourceControlErrorCode.ContentUnsafe, (await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
            fixture.Runner.BuildCommitAsync(fixture.Workspace, binding with { ModelRoots = ["Models"] },
                command with { Snapshots = [unsafeSnapshot] }, accepted, TestContext.Current.CancellationToken))).Code);
        Assert.Equal(SourceControlErrorCode.InvalidInput, (await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
            fixture.Runner.BuildCommitAsync(fixture.Workspace, binding, command with { WorkBranch = "master" },
                accepted, TestContext.Current.CancellationToken))).Code);
    }

    [Fact]
    public async Task Accepted_revision_fetch_does_not_substitute_a_moved_default_branch()
    {
        await using var fixture = await HttpsFixture.CreateAsync(hostile: true);
        var accepted = await fixture.MoveDefaultBranchAsync();
        using var lease = new GitHubTokenLease(fixture.Token, DateTimeOffset.UtcNow.AddMinutes(2));
        await fixture.Runner.FetchLocalAcceptanceAsync(fixture.Workspace, fixture.Remote, "master", lease,
            fixture.CaFile, TestContext.Current.CancellationToken, accepted);
        var repository = "--git-dir=" + Path.Combine(fixture.Workspace.Directory, "repository.git");
        Assert.Equal(accepted.Value, Encoding.UTF8.GetString(await fixture.GitAsync([repository, "rev-parse", "refs/heads/vertex-source"])).Trim());
        Assert.Equal(fixture.ModelBytes, await fixture.GitAsync([repository, "show", accepted.Value + ":models/example.bpmn"]));
        Assert.False(File.Exists(fixture.ExecutionMarker));
    }

    [Fact]
    public async Task Revision_bound_model_read_preserves_bytes_and_rejects_paths_and_limits()
    {
        await using var fixture = await HttpsFixture.CreateAsync(hostile: true);
        using var lease = new GitHubTokenLease(fixture.Token, DateTimeOffset.UtcNow.AddMinutes(2));
        var token = TestContext.Current.CancellationToken;
        await fixture.Runner.FetchLocalAcceptanceAsync(fixture.Workspace, fixture.Remote, "master", lease, fixture.CaFile, token);
        var repository = "--git-dir=" + Path.Combine(fixture.Workspace.Directory, "repository.git");
        var commit = new GitCommitId(Encoding.UTF8.GetString(await fixture.GitAsync([repository, "rev-parse", "refs/heads/vertex-source"])).Trim());
        var binding = new RepositoryBinding(Guid.NewGuid(), "test", fixture.Remote, null, "master", "release", ["models", "Models", "modules"]);
        var generation = Guid.NewGuid();
        var request = new FileReadRequest(commit, "models/example.bpmn");
        var snapshot = await fixture.Runner.ReadModelAsync(fixture.Workspace, binding, request, generation, token);
        Assert.Equal(fixture.ModelBytes, snapshot.CopyContent());
        Assert.Equal(generation, snapshot.DocumentGeneration);
        Assert.Equal(0, snapshot.LocalRevision);
        Assert.False(File.Exists(fixture.ExecutionMarker));
        Assert.False(Directory.Exists(Path.Combine(fixture.Workspace.Directory, "models")));
        foreach (var (path, expected) in new[]
        {
            ("models/missing.bpmn", SourceControlErrorCode.NotFound),
            ("models/link.bpmn", SourceControlErrorCode.ContentUnsafe),
            ("modules/unsafe/example.bpmn", SourceControlErrorCode.ContentUnsafe),
            ("Models/example.bpmn", SourceControlErrorCode.ContentUnsafe),
            ("models/../example.bpmn", SourceControlErrorCode.InvalidInput),
            ("other/example.bpmn", SourceControlErrorCode.InvalidInput)
        })
        {
            var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
                fixture.Runner.ReadModelAsync(fixture.Workspace, binding, request with { Path = path }, generation, token));
            Assert.Equal(expected, error.Code);
        }
        var limited = new ControlledGitProcess(Options.Create(new SourceControlOptions
        {
            Enabled = true, GitExecutablePath = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_GIT"),
            Limits = new() { MaxModelBytes = 8 }
        }));
        var oversized = await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
            limited.ReadModelAsync(fixture.Workspace, binding, request, generation, token));
        Assert.Equal(SourceControlErrorCode.PayloadTooLarge, oversized.Code);
    }

    [Fact]
    public async Task Model_read_uses_model_byte_limit_and_fixed_commit_not_moving_ref()
    {
        await using var fixture = await HttpsFixture.CreateAsync();
        using var lease = new GitHubTokenLease(fixture.Token, DateTimeOffset.UtcNow.AddMinutes(2));
        var token = TestContext.Current.CancellationToken;
        await fixture.Runner.FetchLocalAcceptanceAsync(fixture.Workspace, fixture.Remote, "master", lease, fixture.CaFile, token);
        var repository = "--git-dir=" + Path.Combine(fixture.Workspace.Directory, "repository.git");
        var basis = new GitCommitId(Encoding.UTF8.GetString(await fixture.GitAsync([repository, "rev-parse", "refs/heads/vertex-source"])).Trim());
        var binding = new RepositoryBinding(Guid.NewGuid(), "test", fixture.Remote, null, "master", "release", ["models"]);
        var generation = Guid.NewGuid();
        var session = Guid.NewGuid();
        var bytes = Encoding.UTF8.GetBytes("<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\">"
            + new string(' ', 1024 * 1024) + "<process id=\"large\"/></definitions>\r\n");
        var command = new CommitCommand(Guid.NewGuid(), new SourceControlIdempotencyKey("large-read"), session,
            basis, SourceControlInputPolicy.WorkBranch(session), "Large model",
            [new ModelSnapshot("models/example.bpmn", SourceModelKind.Bpmn, generation, 1, bytes)]);
        var commit = await fixture.Runner.BuildCommitAsync(fixture.Workspace, binding, command, DateTimeOffset.UtcNow, token);
        await fixture.Runner.PublishLocalCommitAsync(fixture.Workspace, command.WorkBranch, commit, token);
        await fixture.GitAsync([repository, "update-ref", "refs/replace/" + basis.Value, commit.Value]);
        var current = await fixture.Runner.ReadModelAsync(fixture.Workspace, binding,
            new(commit, "models/example.bpmn"), generation, token);
        var original = await fixture.Runner.ReadModelAsync(fixture.Workspace, binding,
            new(basis, "models/example.bpmn"), generation, token);
        Assert.Equal(bytes, current.CopyContent());
        Assert.Equal(fixture.ModelBytes, original.CopyContent());
        Assert.Equal(bytes, await fixture.GitAsync([repository, "cat-file", "blob", commit.Value + ":models/example.bpmn"]));
    }

    private sealed class HttpsFixture : IAsyncDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "vertex-git-https-" + Guid.NewGuid().ToString("N"));
        private readonly string _git;
        private readonly bool _stall;
        private readonly bool _hostile;
        private WebApplication? _server;
        private WebApplication? _revocationServer;
        private X509Certificate2? _certificate;
        private X509Certificate2? _issuer;
        internal string Token { get; } = "test-only-" + Guid.NewGuid().ToString("N");
        internal int Challenges;
        internal int AuthenticatedRequests;
        internal TaskCompletionSource RequestStalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource RequestClosed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal byte[] ModelBytes { get; } = Encoding.UTF8.GetBytes("<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\">\r\n<process id=\"exact\"/>\r\n</definitions>");
        internal GitWorkspace Workspace { get; private set; } = null!;
        internal ControlledGitProcess Runner { get; private set; } = null!;
        internal Uri Remote { get; private set; } = null!;
        internal string CaFile { get; private set; } = null!;
        internal string ExecutionMarker => Path.Combine(_root, "untrusted-program-executed");

        private HttpsFixture(bool stall, bool hostile)
        {
            _stall = stall;
            _hostile = hostile;
            _git = Environment.GetEnvironmentVariable("VERTEXBPMN_TEST_GIT")!;
            Assert.True(_git is not null && Path.IsPathFullyQualified(_git) && File.Exists(_git), "Configure the real Git executable for local acceptance.");
        }

        internal static async Task<HttpsFixture> CreateAsync(bool stall = false, bool hostile = false)
        {
            var fixture = new HttpsFixture(stall, hostile);
            try { await fixture.StartAsync(); return fixture; }
            catch { await fixture.DisposeAsync(); throw; }
        }

        private async Task StartAsync()
        {
            var source = Path.Combine(_root, "source");
            Directory.CreateDirectory(source);
            var modelDirectory = Path.Combine(source, "models");
            Directory.CreateDirectory(modelDirectory);
            await File.WriteAllBytesAsync(Path.Combine(modelDirectory, "example.bpmn"), ModelBytes, TestContext.Current.CancellationToken);
            await GitAsync(["init", "--initial-branch=master", "--template=", source]);
            await GitAsync(["-C", source, "add", "--", "models/example.bpmn"]);
            await GitAsync(["-C", source, "-c", "user.name=Acceptance", "-c", "user.email=acceptance@example.invalid", "commit", "-m", "fixture"]);
            if (_hostile)
            {
                await File.WriteAllTextAsync(Path.Combine(source, ".gitattributes"), "models/*.bpmn filter=untrusted diff=untrusted\n", TestContext.Current.CancellationToken);
                await File.WriteAllTextAsync(Path.Combine(source, ".gitmodules"), "[submodule \"unsafe\"]\npath = modules/unsafe\nurl = https://127.0.0.1:1/forbidden.git\n", TestContext.Current.CancellationToken);
                var oid = Encoding.UTF8.GetString(await GitAsync(["-C", source, "rev-parse", "HEAD"])).Trim();
                await GitAsync(["-C", source, "add", "--", ".gitattributes", ".gitmodules"]);
                await GitAsync(["-C", source, "update-index", "--add", "--cacheinfo", "160000", oid, "modules/unsafe"]);
                var blob = Encoding.UTF8.GetString(await GitAsync(["-C", source, "rev-parse", "HEAD:models/example.bpmn"])).Trim();
                // A link-mode tree entry is enough to verify that protected readers never follow it.
                await GitAsync(["-C", source, "update-index", "--add", "--cacheinfo", "120000", blob, "models/link.bpmn"]);
                await GitAsync(["-C", source, "-c", "user.name=Acceptance", "-c", "user.email=acceptance@example.invalid", "commit", "-m", "hostile metadata"]);
            }
            var repositories = Path.Combine(_root, "remote");
            Directory.CreateDirectory(repositories);
            await GitAsync(["clone", "--bare", "--no-local", source, Path.Combine(repositories, "models.git")]);
            var client = Path.Combine(_root, "client");
            var control = Path.Combine(client, "control");
            Directory.CreateDirectory(Path.Combine(control, "hooks"));
            Workspace = new(client, control, Guid.NewGuid(), 1);
            var helper = Path.Combine(AppContext.BaseDirectory, "auth-helper", "VertexBPMN.SourceControl.AuthHelper" + (OperatingSystem.IsWindows() ? ".exe" : ""));
            Assert.True(File.Exists(helper));
            Runner = new(Options.Create(new SourceControlOptions { Enabled = true, GitExecutablePath = _git,
                AuthHelperExecutablePath = helper, Limits = new() { WriteTimeout = TimeSpan.FromSeconds(10) } }));
            await Runner.InitializeAsync(Workspace, TestContext.Current.CancellationToken);
            if (_hostile)
            {
                var hooks = Path.Combine(_root, "unsafe-hooks");
                Directory.CreateDirectory(hooks);
                var hook = Path.Combine(hooks, "reference-transaction");
                var script = "#!/bin/sh\nprintf executed > '" + ExecutionMarker.Replace('\\', '/') + "'\n";
                await File.WriteAllTextAsync(hook, script, TestContext.Current.CancellationToken);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(hook, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                var repository = "--git-dir=" + Path.Combine(client, "repository.git");
                await GitAsync([repository, "config", "core.hooksPath", hooks.Replace('\\', '/')]);
                await GitAsync([repository, "config", "core.fsmonitor", hook.Replace('\\', '/')]);
                await GitAsync([repository, "config", "filter.untrusted.smudge", hook.Replace('\\', '/')]);
                await GitAsync([repository, "config", "diff.untrusted.command", hook.Replace('\\', '/')]);
            }

            using var rsa = RSA.Create(2048);
            using var issuerKey = RSA.Create(2048);
            var issuerRequest = new CertificateRequest("CN=Vertex isolated test CA " + Guid.NewGuid().ToString("N"), issuerKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            issuerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            issuerRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
            issuerRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(issuerRequest.PublicKey, false));
            _issuer = issuerRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(2));
            var issuer = _issuer;
            // Supply a real signed CRL rather than disabling Schannel revocation checks.
            var crl = new CertificateRevocationListBuilder().Build(issuer, System.Numerics.BigInteger.One,
                DateTimeOffset.UtcNow.AddDays(1), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1,
                DateTimeOffset.UtcNow.AddHours(-1));
            var revocationBuilder = WebApplication.CreateBuilder();
            revocationBuilder.Logging.ClearProviders();
            revocationBuilder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
            _revocationServer = revocationBuilder.Build();
            _revocationServer.MapGet("/issuer.crl", () => Results.Bytes(crl, "application/pkix-crl"));
            await _revocationServer.StartAsync(TestContext.Current.CancellationToken);
            var crlAddress = _revocationServer.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            var certRequest = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            certRequest.CertificateExtensions.Add(CertificateRevocationListBuilder.BuildCrlDistributionPointExtension([crlAddress + "/issuer.crl"]));
            certRequest.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
            var names = new SubjectAlternativeNameBuilder();
            names.AddDnsName("localhost");
            certRequest.CertificateExtensions.Add(names.Build());
            certRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            certRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
            certRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
            using var signed = certRequest.Create(issuer, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), RandomNumberGenerator.GetBytes(16));
            using var generated = signed.CopyWithPrivateKey(rsa);
            // Windows Schannel cannot serve TLS with ephemeral private-key handles.
            // The fixture owns/disposes the imported certificate; it is not added to a trust store.
            _certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
            CaFile = Path.Combine(control, "test-ca.pem");
            await File.WriteAllTextAsync(CaFile, issuer.ExportCertificatePem(), TestContext.Current.CancellationToken);
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders(); // Never request/auth logging in the credential fixture.
            builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(https =>
            {
                https.ServerCertificate = _certificate;
                https.ServerCertificateChain = new X509Certificate2Collection(issuer);
            })));
            _server = builder.Build();
            _server.Run(context => HandleAsync(context, repositories));
            await _server.StartAsync(TestContext.Current.CancellationToken);
            var address = _server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            Remote = new Uri(address.Replace("127.0.0.1", "localhost", StringComparison.Ordinal) + "/models.git");
        }

        private async Task HandleAsync(HttpContext context, string repositories)
        {
            var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("x-access-token:" + Token));
            if (context.Request.Headers.Authorization != expected)
            {
                Interlocked.Increment(ref Challenges);
                context.Response.StatusCode = 401;
                context.Response.Headers.WWWAuthenticate = "Basic realm=\"isolated-git-test\"";
                return;
            }
            Interlocked.Increment(ref AuthenticatedRequests);
            if (_stall)
            {
                RequestStalled.TrySetResult();
                try { await Task.Delay(Timeout.Infinite, context.RequestAborted); }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
                finally { RequestClosed.TrySetResult(); }
                return;
            }
            var start = StartInfo(["http-backend"]);
            start.Environment["GIT_PROJECT_ROOT"] = repositories;
            start.Environment["GIT_HTTP_EXPORT_ALL"] = "1";
            start.Environment["PATH_INFO"] = context.Request.Path.Value!;
            start.Environment["QUERY_STRING"] = context.Request.QueryString.Value?.TrimStart('?') ?? "";
            start.Environment["REQUEST_METHOD"] = context.Request.Method;
            start.Environment["CONTENT_TYPE"] = context.Request.ContentType ?? "";
            start.Environment["CONTENT_LENGTH"] = context.Request.ContentLength?.ToString() ?? "";
            start.Environment["REMOTE_USER"] = "isolated-acceptance";
            start.Environment["HTTP_GIT_PROTOCOL"] = context.Request.Headers["Git-Protocol"].ToString();
            using var process = Process.Start(start)!;
            var errors = process.StandardError.ReadToEndAsync(context.RequestAborted);
            var input = Task.Run(async () =>
            {
                await context.Request.Body.CopyToAsync(process.StandardInput.BaseStream, context.RequestAborted);
                process.StandardInput.Close();
            }, context.RequestAborted);
            try
            {
                // CGI headers must not consume binary pack bytes in a buffered text reader.
                while (true)
                {
                    var line = await ReadCgiHeaderAsync(process.StandardOutput.BaseStream, context.RequestAborted);
                    if (line.Length == 0) break;
                    var split = line.IndexOf(':');
                    Assert.True(split > 0);
                    if (line[..split] == "Status") context.Response.StatusCode = int.Parse(line[(split + 1)..].Trim().Split(' ')[0]);
                    else context.Response.Headers[line[..split]] = line[(split + 1)..].Trim();
                }
                await process.StandardOutput.BaseStream.CopyToAsync(context.Response.Body, context.RequestAborted);
                await input;
                await process.WaitForExitAsync(context.RequestAborted);
                _ = await errors;
                Assert.Equal(0, process.ExitCode);
            }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        }

        private static async Task<string> ReadCgiHeaderAsync(Stream stream, CancellationToken cancellationToken)
        {
            var bytes = new List<byte>();
            var buffer = new byte[1];
            while (await stream.ReadAsync(buffer, cancellationToken) != 0)
            {
                if (buffer[0] == 10) return Encoding.UTF8.GetString(bytes.ToArray()).TrimEnd('\r');
                Assert.True(bytes.Count < 8192);
                bytes.Add(buffer[0]);
            }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private ProcessStartInfo StartInfo(IReadOnlyList<string> arguments)
        {
            var start = new ProcessStartInfo(_git) { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            foreach (var key in start.Environment.Keys.Where(x => x.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToArray())
                start.Environment.Remove(key);
            start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            start.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(_root, "no-global-config");
            start.Environment["GIT_TERMINAL_PROMPT"] = "0";
            return start;
        }

        internal async Task<GitCommitId> MoveDefaultBranchAsync()
        {
            var repository = "--git-dir=" + Path.Combine(_root, "remote", "models.git");
            var accepted = new GitCommitId(Encoding.UTF8.GetString(await GitAsync([repository, "rev-parse", "refs/heads/master"])).Trim());
            var previous = Encoding.UTF8.GetString(await GitAsync([repository, "rev-parse", accepted.Value + "^"])).Trim();
            await GitAsync([repository, "update-ref", "refs/heads/alternate", accepted.Value]);
            await GitAsync([repository, "update-ref", "refs/heads/master", previous, accepted.Value]);
            return accepted;
        }

        internal async Task<byte[]> GitAsync(IReadOnlyList<string> arguments)
        {
            using var process = Process.Start(StartInfo(arguments))!;
            process.StandardInput.Close();
            var error = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            using var buffer = new MemoryStream();
            var output = process.StandardOutput.BaseStream.CopyToAsync(buffer, TestContext.Current.CancellationToken);
            await Task.WhenAll(output, process.WaitForExitAsync(TestContext.Current.CancellationToken));
            _ = await error;
            Assert.Equal(0, process.ExitCode);
            return buffer.ToArray();
        }

        public async ValueTask DisposeAsync()
        {
            if (_server is not null) { await _server.StopAsync(CancellationToken.None); await _server.DisposeAsync(); }
            if (_revocationServer is not null) { await _revocationServer.StopAsync(CancellationToken.None); await _revocationServer.DisposeAsync(); }
            _certificate?.Dispose();
            _issuer?.Dispose();
            if (!Directory.Exists(_root)) return;
            _ = SourceControlWorkspace.MeasureBytes(_root, long.MaxValue);
            // Only this generated fixture root; no links or user repositories.
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_root, recursive: true);
        }
    }
}
