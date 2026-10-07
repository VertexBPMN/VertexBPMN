using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class SourceControlSecurityTests
{
    private static readonly RepositoryBinding Binding = new(Guid.NewGuid(), "tenant-a",
        new Uri("https://github.com/example/models.git"), "credential-id", "master", "release", ["models"]);
    private static readonly SourceControlContext Context = new("tenant-a", "issuer|alice");

    [Theory]
    [InlineData("../secret.bpmn")]
    [InlineData("models/../secret.bpmn")]
    [InlineData("/models/a.bpmn")]
    [InlineData("C:/models/a.bpmn")]
    [InlineData("models\\a.bpmn")]
    [InlineData("models/.GIT/config")]
    [InlineData("models/NUL.bpmn")]
    [InlineData("models/COM1.bpmn")]
    [InlineData("models/a.bpmn:secret")]
    [InlineData("models/%2e%2e/a.bpmn")]
    [InlineData("models//a.bpmn")]
    [InlineData("models/a./file.bpmn")]
    [InlineData("models/a /file.bpmn")]
    [InlineData("models/.gitmodules")]
    public void Unsafe_paths_are_rejected_without_echoing_input(string path)
    {
        var error = Assert.Throws<SourceControlSecurityException>(() => SourceControlInputPolicy.ValidateRelativePath(path));
        Assert.Equal(SourceControlErrorCode.InvalidInput, error.Code);
        Assert.DoesNotContain(path, error.ToString());
    }

    [Theory]
    [InlineData("models2/a.bpmn")]
    [InlineData("Models/a.bpmn")]
    [InlineData("models/a.dll")]
    public void Model_paths_require_exact_root_boundary_and_bpmn(string path) =>
        Assert.Throws<SourceControlSecurityException>(() => SourceControlInputPolicy.DemandModelPath(path, Binding.ModelRoots));

    [Theory]
    [InlineData("--upload-pack=evil")]
    [InlineData("main:other")]
    [InlineData("main..other")]
    [InlineData("@{-1}")]
    [InlineData("main.lock")]
    [InlineData("feature/.hidden")]
    [InlineData("main\\evil")]
    [InlineData("main/")]
    [InlineData("HEAD")]
    [InlineData("main\nsecret")]
    public void Ref_expressions_and_options_are_rejected(string branch) =>
        Assert.Throws<SourceControlSecurityException>(() => SourceControlInputPolicy.ValidateBranch(branch));

    [Fact]
    public void Work_branch_is_server_bound_not_a_user_refspec()
    {
        var id = Guid.NewGuid();
        var workBranch = SourceControlInputPolicy.WorkBranch(id);
        var session = Session(id, workBranch);
        SourceControlInputPolicy.DemandWorkBranch(Binding, session, workBranch);
        Assert.Throws<SourceControlSecurityException>(() => SourceControlInputPolicy.DemandWorkBranch(Binding, session, "master"));
        Assert.Throws<SourceControlSecurityException>(() => SourceControlInputPolicy.DemandWorkBranch(Binding, session, SourceControlInputPolicy.WorkBranch(Guid.NewGuid())));
    }

    [Theory]
    [InlineData("Admin", RepositoryPermission.Read, RepositoryPermission.Commit, SourceControlErrorCode.Forbidden)]
    [InlineData("ReadOnly", RepositoryPermission.Read | RepositoryPermission.Commit, RepositoryPermission.Commit, SourceControlErrorCode.Forbidden)]
    [InlineData("ProcessManager", RepositoryPermission.Read | RepositoryPermission.Manage, RepositoryPermission.Manage, SourceControlErrorCode.Forbidden)]
    [InlineData("ExternalTaskWorker", RepositoryPermission.Read, RepositoryPermission.Read, SourceControlErrorCode.NotFound)]
    [InlineData("Admin", RepositoryPermission.Manage, RepositoryPermission.Manage, SourceControlErrorCode.NotFound)]
    [InlineData("Admin", RepositoryPermission.None, RepositoryPermission.Read, SourceControlErrorCode.NotFound)]
    public void Roles_and_grants_are_independent(string role, RepositoryPermission grant, RepositoryPermission request, SourceControlErrorCode expected)
    {
        var error = Assert.Throws<SourceControlSecurityException>(() => RepositoryAccessPolicy.Demand(
            Context, Binding, [role], [new(Context.ActorId, grant)], request));
        Assert.Equal(expected, error.Code);
    }

    [Fact]
    public void Rights_are_reevaluated_and_cross_tenant_admin_cannot_bypass()
    {
        RepositoryAccessPolicy.Demand(Context, Binding, ["ProcessManager"],
            [new(Context.ActorId, RepositoryPermission.Read | RepositoryPermission.Commit)], RepositoryPermission.Commit);
        Assert.Throws<SourceControlSecurityException>(() => RepositoryAccessPolicy.Demand(Context, Binding,
            ["ProcessManager"], [], RepositoryPermission.Commit));
        var error = Assert.Throws<SourceControlSecurityException>(() => RepositoryAccessPolicy.Demand(
            new SourceControlContext("tenant-b", Context.ActorId), Binding, ["Admin"],
            [new(Context.ActorId, RepositoryPermission.Read | RepositoryPermission.Manage)], RepositoryPermission.Manage));
        Assert.Equal(SourceControlErrorCode.NotFound, error.Code);
    }

    [Fact]
    public void Session_owner_repository_and_expiry_are_checked()
    {
        var id = Guid.NewGuid();
        var session = Session(id, SourceControlInputPolicy.WorkBranch(id));
        RepositoryAccessPolicy.DemandSessionOwner(Context, Binding, session, DateTimeOffset.UtcNow);
        Assert.Throws<SourceControlSecurityException>(() => RepositoryAccessPolicy.DemandSessionOwner(
            Context, Binding, session with { ActorId = "issuer|bob" }, DateTimeOffset.UtcNow));
        Assert.Throws<SourceControlSecurityException>(() => RepositoryAccessPolicy.DemandSessionOwner(
            Context, Binding, session with { RepositoryId = Guid.NewGuid() }, DateTimeOffset.UtcNow));
        Assert.Throws<SourceControlSecurityException>(() => RepositoryAccessPolicy.DemandSessionOwner(
            Context, Binding, session with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) }, DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData("<password>test-secret</password>")]
    [InlineData("<property name=\"api_key\" value=\"test-secret\"/>")]
    [InlineData("<serviceTask url=\"https://example.com/?access_token=test-secret\"/>")]
    [InlineData("<script>[VERTEX-REDACTED]</script>")]
    public void Structured_secrets_are_blocked_not_silently_redacted(string content)
    {
        var snapshot = Snapshot($"<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"><process id=\"p\">{content}</process></definitions>");
        var original = snapshot.CopyContent();
        var error = Assert.Throws<SourceControlSecurityException>(() => SourceControlInputPolicy.DemandSafeBpmn(snapshot, Binding, new()));
        Assert.Equal(SourceControlErrorCode.ContentUnsafe, error.Code);
        Assert.Equal(original, snapshot.CopyContent());
        Assert.DoesNotContain("test-secret", error.ToString());
    }

    [Theory]
    [InlineData("<!DOCTYPE definitions [<!ENTITY x SYSTEM 'file:///secret'>]><definitions>&x;</definitions>")]
    [InlineData("<definitions>")]
    [InlineData("<wrong/>")]
    public void Unsafe_xml_is_rejected(string xml) =>
        Assert.Equal(SourceControlErrorCode.ContentUnsafe, Assert.Throws<SourceControlSecurityException>(() =>
            SourceControlInputPolicy.DemandSafeBpmn(Snapshot(xml), Binding, new())).Code);

    [Fact]
    public void Safe_draft_preserves_bytes_and_does_not_require_executable_semantics()
    {
        var bytes = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?>\r\n<definitions xmlns=\"http://www.omg.org/spec/BPMN/20100524/MODEL\"><process id=\"draft\"><serviceTask credentialRef=\"credential-id\"/></process></definitions>");
        var snapshot = new ModelSnapshot("models/Entwurf.bpmn", SourceModelKind.Bpmn, Guid.NewGuid(), 1, bytes);
        SourceControlInputPolicy.DemandSafeBpmn(snapshot, Binding, new());
        Assert.Equal(bytes, snapshot.CopyContent());
        Assert.Equal(SourceControlErrorCode.PayloadTooLarge, Assert.Throws<SourceControlSecurityException>(() =>
            SourceControlInputPolicy.DemandSafeBpmn(snapshot, Binding, new() { MaxModelBytes = bytes.Length - 1 })).Code);
    }

    private static ModelSnapshot Snapshot(string xml) => new("models/model.bpmn", SourceModelKind.Bpmn, Guid.NewGuid(), 0, Encoding.UTF8.GetBytes(xml));
    private static EditSession Session(Guid id, string workBranch) => new(id, Binding.Id, Context.TenantId,
        Context.ActorId, workBranch, new GitCommitId(new string('a', 40)), Guid.NewGuid(), 1, null, DateTimeOffset.UtcNow.AddHours(1));
}
