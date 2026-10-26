using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Infrastructure.Persistence.Services;

namespace VertexBPMN.Tests.Unit.Infrastructure;

/// <summary>
/// S5 + LC-AC-09 slice: authoring a reference application entirely through the LC07
/// designer serializes process references as { type, key }. The server must resolve that
/// stable KEY to the real ProcessDefinition id+tenantId at the designer/server boundary so
/// persisted content satisfies the metadata gate and releases, while the runtime still
/// starts the process by key. These tests pin that boundary behaviour against the real
/// persistent service.
/// </summary>
public sealed class PersistentApplicationDefinitionS5Tests
{
    private static ApplicationAccessContext Access { get; } = new(
        "s5-editor",
        new HashSet<string>([ "application.design", "application.publish" ], StringComparer.Ordinal));

    private const string Tenant = "tenant-a";
    private const string ProcessKey = "lc07-invoice-flow";

    // Designer-authored content: a searchable table bound to a declared data source (with a
    // sanctioned data-source-read action so the released read is served), a data mapping, a
    // form-js component whose action starts the process, and a process reference serialized
    // as { type, key } by AppDesignerDocument.ToJson.
    private const string DesignerContent = """
        {
          "localizations": {},
          "references": [ { "type": "process", "key": "lc07-invoice-flow" } ],
          "dataSources": [ { "key": "invoices", "bindingVersion": 2, "fields": [ "id", "amount" ] } ],
          "pages": [
            {
              "id": "p1",
              "route": "/invoices",
              "components": [
                { "id": "c1", "type": "table", "version": "1.0.0", "dataSource": "invoices",
                  "mappings": [ { "sourcePath": "amount", "targetKey": "Betrag" } ],
                  "action": { "type": "data-source-read", "version": "1.0.0", "dataSource": "invoices" } },
                { "id": "c2", "type": "form-js", "version": "1.0.0",
                  "action": { "type": "start-process", "version": "1.0.0", "processDefinitionId": "lc07-invoice-flow" } }
              ]
            }
          ]
        }
        """;

    private static async Task<ProcessDefinition> SeedProcessAsync(BpmnDbContext db, string key, string tenant)
    {
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "s5-deployment", CreatedAt = DateTime.UtcNow };
        var process = new ProcessDefinition
        {
            Id = Guid.NewGuid(),
            Key = key,
            Name = "Invoice flow",
            Version = 1,
            TenantId = tenant,
            Deployment = deployment,
            DeploymentId = deployment.Id,
            BpmnXml = InvoiceFlowBpmn
        };
        db.EngineDeployments.Add(deployment);
        db.ProcessDefinitions.Add(process);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return process;
    }

    private const string InvoiceFlowBpmn = """
        <?xml version="1.0" encoding="UTF-8"?>
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                     targetNamespace="https://vertexbpmn.dev/s5">
          <process id="lc07-invoice-flow" isExecutable="true">
            <startEvent id="start" />
            <sequenceFlow id="f1" sourceRef="start" targetRef="approve" />
            <userTask id="approve" name="Freigabe">
              <extensionElements>
                <vertex:user xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0"
                             candidateRoles="FinanceAgent" />
              </extensionElements>
            </userTask>
            <sequenceFlow id="f2" sourceRef="approve" targetRef="end" />
            <endEvent id="end" />
          </process>
        </definitions>
        """;

    [Fact]
    public async Task Create_ResolvesProcessKeyToIdAndTenant_PassesGateAndPublishes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var process = await SeedProcessAsync(db, ProcessKey, Tenant);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);

        // Authoring through the designer must NOT be rejected: the server resolves the key.
        var draft = await service.CreateAsync(Tenant, new("lc07-invoice-app", "Invoices", 0, DesignerContent), TestContext.Current.CancellationToken);

        var persisted = await service.GetRevisionAsync(Tenant, draft.Id, 1, TestContext.Current.CancellationToken);
        Assert.NotNull(persisted);
        using var doc = JsonDocument.Parse(persisted.ContentJson);
        var reference = Assert.Single(doc.RootElement.GetProperty("references").EnumerateArray());
        Assert.Equal("process", reference.GetProperty("type").GetString());
        Assert.Equal(ProcessKey, reference.GetProperty("key").GetString());
        Assert.Equal(process.Id.ToString(), reference.GetProperty("id").GetString());
        Assert.Equal(Tenant, reference.GetProperty("tenantId").GetString());

        // The persisted (enriched) content satisfies the domain diagnostics gate.
        Assert.Equal("[]", ApplicationDomainDiagnostics.Validate(persisted.ContentJson));

        // The release pipeline accepts and releases the designer-built content.
        var candidate = await service.CreateReleaseCandidateAsync(Tenant, draft.Id, 1, TestContext.Current.CancellationToken);
        Assert.True(await service.ReleaseAsync(Tenant, candidate.Id, TestContext.Current.CancellationToken));
        var released = await service.GetReleasedRevisionAsync(Tenant, draft.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(released);
    }

    [Fact]
    public async Task Update_WithDesignerBuiltContent_AlsoResolvesProcessKey()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var process = await SeedProcessAsync(db, ProcessKey, Tenant);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);
        var draft = await service.CreateAsync(Tenant, new("lc07-invoice-app", "Invoices", 0, "{}"), TestContext.Current.CancellationToken);

        await service.UpdateAsync(Tenant, draft.Id, new("lc07-invoice-app", "Invoices v2", 1, DesignerContent), TestContext.Current.CancellationToken);

        var revision = await service.GetRevisionAsync(Tenant, draft.Id, 2, TestContext.Current.CancellationToken);
        using var doc = JsonDocument.Parse(revision!.ContentJson);
        var reference = Assert.Single(doc.RootElement.GetProperty("references").EnumerateArray());
        Assert.Equal(process.Id.ToString(), reference.GetProperty("id").GetString());
        Assert.Equal("[]", ApplicationDomainDiagnostics.Validate(revision.ContentJson));
    }

    [Fact]
    public async Task Create_UnknownProcessKey_IsStillRejectedByGate()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var service = new PersistentApplicationDefinitionService(db, Mock.Of<IAuditLogService>(), access: Access);

        const string content = """{"references":[{"type":"process","key":"no-such-flow"}]}""";
        await Assert.ThrowsAsync<ApplicationValidationException>(
            () => service.CreateAsync(Tenant, new("x", "X", 0, content), TestContext.Current.CancellationToken));
    }
}
