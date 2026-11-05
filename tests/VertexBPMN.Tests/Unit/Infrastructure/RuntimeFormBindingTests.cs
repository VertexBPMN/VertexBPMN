using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class RuntimeFormBindingTests
{
    private static readonly string[] ExpectedAmountApproved = new[] { "amount", "approvedBy" };
    private static readonly string[] ReviewerLocal = new[] { "local-value" };
    private static readonly string[] ExpectedReviewer = new[] { "reviewer" };

    [Fact]
    public async Task TaskCreation_FreezesSchemaPrefillIncludingNestedReadOnlyRequiredFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var forms = new PersistentFormDefinitionService(db, Mock.Of<IAuditLogService>());
        const string schema = """
        {"components":[{"type":"group","readonly":true,"components":[{"type":"textfield","key":"approvedBy","validate":{"required":true}}]},{"type":"number","key":"amount"}]}
        """;
        await forms.CreateAsync("tenant-a", new("approval", "Approval", schema), TestContext.Current.CancellationToken);
        var definition = await AddDefinitionAsync(db, "<userTask id='t' camunda:formRef='approval'/>");
        var runtime = CreateRuntime(db);
        var instance = await runtime.StartAsync(definition, new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["approvedBy"] = "trusted-reviewer", ["amount"] = 42, ["secret"] = "do-not-disclose"
        }, null, "tenant-a", cancellationToken: TestContext.Current.CancellationToken);
        var task = Assert.Single(await db.Tasks.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ExpectedAmountApproved, task.LocalVariables.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal("trusted-reviewer", task.LocalVariables["approvedBy"].ToString());
        Assert.Equal("42", task.LocalVariables["amount"].ToString());
        instance.Variables["approvedBy"] = "changed-after-creation";
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        task = await db.Tasks.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("trusted-reviewer", task.LocalVariables["approvedBy"].ToString());
        Assert.True(VertexBPMN.Domain.Forms.TaskFormValidator.Validate(task, new Dictionary<string, object>(StringComparer.Ordinal) { ["amount"] = 43 }).IsValid);
    }

    [Fact]
    public async Task TaskCreation_PrefillDoesNotShareMutableInstanceValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await new PersistentFormDefinitionService(db, Mock.Of<IAuditLogService>()).CreateAsync("tenant-a",
            new("approval", "Approval", "{\"components\":[{\"type\":\"textfield\",\"key\":\"details\"}]}"), TestContext.Current.CancellationToken);
        var definition = await AddDefinitionAsync(db, "<userTask id='t' camunda:formRef='approval'/>");
        var details = new Dictionary<string, object>(StringComparer.Ordinal) { ["value"] = "original" };
        await CreateRuntime(db).StartAsync(definition, new Dictionary<string, object>(StringComparer.Ordinal) { ["details"] = details },
            null, "tenant-a", cancellationToken: TestContext.Current.CancellationToken);
        var task = await db.Tasks.SingleAsync(TestContext.Current.CancellationToken);
        details["value"] = "changed";
        Assert.Equal("original", System.Text.Json.JsonSerializer.SerializeToElement(task.LocalVariables["details"]).GetProperty("value").GetString());
    }

    [Fact]
    public async Task TaskCreation_PendingLocalOverridesInstancePrefillWithoutLeakingLoopLocals()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await new PersistentFormDefinitionService(db, Mock.Of<IAuditLogService>()).CreateAsync("tenant-a",
            new("approval", "Approval", "{\"components\":[{\"type\":\"textfield\",\"key\":\"reviewer\",\"readonly\":true,\"validate\":{\"required\":true}}]}"), TestContext.Current.CancellationToken);
        var definition = await AddDefinitionAsync(db, "<userTask id='t' camunda:formRef='approval'><multiInstanceLoopCharacteristics camunda:collection='reviewers' camunda:elementVariable='reviewer'/></userTask>");
        await CreateRuntime(db).StartAsync(definition, new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["reviewer"] = "global-value", ["reviewers"] = ReviewerLocal, ["secret"] = "secret"
        }, null, "tenant-a", cancellationToken: TestContext.Current.CancellationToken);
        var task = await db.Tasks.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("local-value", task.LocalVariables["reviewer"].ToString());
        Assert.Equal(ExpectedReviewer, task.LocalVariables.Keys.Where(key => !key.StartsWith("$vertex.", StringComparison.Ordinal)), StringComparer.Ordinal);
        Assert.True(VertexBPMN.Domain.Forms.TaskFormValidator.Validate(task, null).IsValid);
    }

    [Theory]
    [InlineData("camunda:formRef='missing' camunda:formRefBinding='latest'")]
    [InlineData("camunda:formRef='app:approval.jsf'")]
    [InlineData("camunda:formRef='' camunda:formKey='app:approval.jsf'")]
    [InlineData("camunda:formKey='missing'")]
    public async Task UnresolvedManagedForm_RejectsTaskCreation(string attributes)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var definition = await AddDefinitionAsync(db, $"<userTask id='t' {attributes}/>");
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRuntime(db).StartAsync(definition, null, null, "tenant-a", cancellationToken: TestContext.Current.CancellationToken).AsTask());
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Tasks.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.ProcessInstances.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("app:approval.jsf")]
    [InlineData("embedded:app:forms/approval.html")]
    public async Task LegacyExternalFormKey_ExplicitResourceRemainsCompatible(string resource)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var definition = await AddDefinitionAsync(db, $"<userTask id='t' camunda:formKey='{resource}'/>");
        await CreateRuntime(db).StartAsync(definition, null, null, "tenant-a", cancellationToken: TestContext.Current.CancellationToken);
        var task = await db.Tasks.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(resource, task.FormKey);
        Assert.Null(task.FormSchema);
        Assert.Null(task.FormRevisionId);
    }

    private static PersistentProcessExecutionRuntime CreateRuntime(BpmnDbContext db) =>
        new(db, Mock.Of<IServiceTaskRegistry>(), Mock.Of<IDecisionService>(), NullLogger<PersistentProcessExecutionRuntime>.Instance);

    private static async Task<ProcessDefinition> AddDefinitionAsync(BpmnDbContext db, string taskXml)
    {
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "binding", TenantId = "tenant-a" };
        db.EngineDeployments.Add(deployment);
        var definition = new ProcessDefinition
        {
            Id = Guid.NewGuid(), Key = "binding", Name = "binding", TenantId = "tenant-a", DeploymentId = deployment.Id,
            BpmnXml = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:camunda='http://camunda.org/schema/1.0/bpmn'><process id='binding'><startEvent id='s'/><sequenceFlow id='f' sourceRef='s' targetRef='t'/>{taskXml}</process></definitions>"
        };
        db.ProcessDefinitions.Add(definition);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return definition;
    }

    [Theory]
    [InlineData("99", "version")]
    [InlineData("invalid", "version")]
    [InlineData(null, "version")]
    [InlineData(null, "release")]
    [InlineData(null, "latest")]
    [InlineData(null, null)]
    public async Task ExplicitVersion_UsesHistoricalSchemaAtTaskCreation_InvalidVersion(string? version, string? binding)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "binding", TenantId = "tenant-a" };
        db.EngineDeployments.Add(deployment);
        var definition = new ProcessDefinition { Id = Guid.NewGuid(), Key = "binding", Name = "binding", TenantId = "tenant-a", DeploymentId = deployment.Id,
            BpmnXml = $"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'><process id='binding'><startEvent id='s'/><sequenceFlow id='f' sourceRef='s' targetRef='t'/><userTask id='t' formRef='missing' {(binding is null ? "" : $"formRefBinding='{binding}'")} {(version is null ? "" : $"formRefVersion='{version}'")}/></process></definitions>" };
        db.ProcessDefinitions.Add(definition);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var runtime = new PersistentProcessExecutionRuntime(db, Mock.Of<IServiceTaskRegistry>(), Mock.Of<IDecisionService>(), NullLogger<PersistentProcessExecutionRuntime>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.StartAsync(definition, null, null, "tenant-a", cancellationToken: TestContext.Current.CancellationToken).AsTask());
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Tasks.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.ProcessInstances.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("version", "1", "{}", 1)]
    [InlineData("latest", null, "{\"new\":true}", 2)]
    [InlineData(null, null, "{\"new\":true}", 2)]
    public async Task ExplicitVersion_UsesHistoricalSchemaAtTaskCreation(string? binding, string? version, string expectedSchema, int expectedVersion)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var forms = new PersistentFormDefinitionService(db, Mock.Of<IAuditLogService>());
        var form = await forms.CreateAsync("tenant-a", new("approval", "Approval", "{}"), TestContext.Current.CancellationToken);
        await forms.UpdateAsync("tenant-a", form.Id, new("approval", "Approval", "{\"new\":true}"), TestContext.Current.CancellationToken);
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "binding", TenantId = "tenant-a" };
        db.EngineDeployments.Add(deployment);
        var definition = new ProcessDefinition
        {
            Id = Guid.NewGuid(), Key = "binding", Name = "binding", TenantId = "tenant-a", DeploymentId = deployment.Id,
            BpmnXml = $$"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:camunda="http://camunda.org/schema/1.0/bpmn" targetNamespace="test">
              <process id="binding" isExecutable="true"><startEvent id="start"/><sequenceFlow id="f" sourceRef="start" targetRef="approve"/>
              <userTask id="approve" camunda:formRef="approval" {{(binding is null ? "" : $"camunda:formRefBinding=\"{binding}\"")}} {{(version is null ? "" : $"camunda:formRefVersion=\"{version}\"")}}/></process>
            </definitions>
            """
        };
        db.ProcessDefinitions.Add(definition);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var runtime = new PersistentProcessExecutionRuntime(db, Mock.Of<IServiceTaskRegistry>(), Mock.Of<IDecisionService>(), NullLogger<PersistentProcessExecutionRuntime>.Instance);
        await runtime.StartAsync(definition, null, null, "tenant-a", cancellationToken: TestContext.Current.CancellationToken);
        var task = Assert.Single(await db.Tasks.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(expectedSchema, task.FormSchema);
        Assert.NotNull(typeof(UserTask).GetProperty("FormRevisionId"));
        Assert.NotNull(typeof(UserTask).GetProperty("FormVersion"));
        Assert.NotNull(typeof(UserTask).GetProperty("FormSchemaHash"));
        var revision = await forms.GetRevisionAsync("tenant-a", form.Id, expectedVersion, TestContext.Current.CancellationToken);
        Assert.Equal(revision!.Id, typeof(UserTask).GetProperty("FormRevisionId")!.GetValue(task));
        Assert.Equal(expectedVersion, typeof(UserTask).GetProperty("FormVersion")!.GetValue(task));
        Assert.Equal(revision.SchemaHash, typeof(UserTask).GetProperty("FormSchemaHash")!.GetValue(task));
        await forms.UpdateAsync("tenant-a", form.Id, new("approval", "Approval", "{\"third\":true}"), TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var persisted = await db.Tasks.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expectedSchema, persisted.FormSchema);
        Assert.Equal(expectedVersion, persisted.FormVersion);
    }
}
