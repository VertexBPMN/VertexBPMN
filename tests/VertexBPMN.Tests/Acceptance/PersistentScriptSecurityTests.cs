using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Acceptance;

public sealed class PersistentScriptSecurityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task StoredDefinition_EnforcesCurrentScriptPolicy(bool waitBeforeScript, bool disableAllScripts)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(ct);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Runtime:Scripts:AllowCSharp"] = waitBeforeScript.ToString(),
            ["Runtime:Scripts:Enabled"] = "true"
        }).Build();
        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "legacy", CreatedAt = DateTime.UtcNow };
        var definition = new ProcessDefinition
        {
            Id = Guid.NewGuid(), Key = "legacy-script", Version = 1, Deployment = deployment,
            DeploymentId = deployment.Id, CreatedAt = DateTime.UtcNow,
            BpmnXml = """
                <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
                  <process id="legacy-script" isExecutable="true">
                    <startEvent id="start"/>
                """ + (waitBeforeScript ? """
                    <sequenceFlow id="wait-flow" sourceRef="start" targetRef="wait"/>
                    <userTask id="wait" name="Wait before script"/>
                    <sequenceFlow id="script-flow" sourceRef="wait" targetRef="script"/>
                """ : """<sequenceFlow id="script-flow" sourceRef="start" targetRef="script"/>""") + """
                    <scriptTask id="script" scriptFormat="C#" resultVariable="result">
                      <script>variables["executed"] = true; return 42;</script>
                    </scriptTask>
                    <sequenceFlow id="end-flow" sourceRef="script" targetRef="end"/>
                    <endEvent id="end"/>
                  </process>
                </definitions>
                """
        };
        // Seed an already deployed definition: intentionally bypass the deployment gate.
        db.ProcessDefinitions.Add(definition);
        await db.SaveChangesAsync(ct);
        var runtime = new PersistentProcessExecutionRuntime(db, Mock.Of<IServiceTaskRegistry>(),
            Mock.Of<IDecisionService>(), NullLogger<PersistentProcessExecutionRuntime>.Instance, configuration);
        var instance = await runtime.StartAsync(definition, null, null, null, cancellationToken: ct);
        if (waitBeforeScript)
        {
            Assert.Equal(ProcessInstanceStatus.Running, instance.Status);
            var task = await db.Tasks.SingleAsync(x => x.ProcessInstanceId == instance.Id, ct);
            configuration[disableAllScripts ? "Runtime:Scripts:Enabled" : "Runtime:Scripts:AllowCSharp"] = "false";
            await runtime.CompleteUserTaskAsync(task.Id, null, cancellationToken: ct);
        }
        Assert.Equal(ProcessInstanceStatus.Suspended, instance.Status);
        Assert.True(await db.Incidents.AnyAsync(x => x.ProcessInstanceId == instance.Id, ct));
        Assert.False(await db.Variables.AnyAsync(x => x.Name == "executed" || x.Name == "result", ct));
    }
}
