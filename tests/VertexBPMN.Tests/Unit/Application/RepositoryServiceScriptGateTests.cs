using Microsoft.Extensions.Configuration;
using Moq;
using VertexBPMN.Application;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Unit.Application;

/// <summary>
/// Phase 3 security acceptance: the Roslyn C# script path is not sandboxed, so
/// Deployment of a C# scriptTask must be rejected unless an operator explicitly
/// enables Runtime:Scripts:AllowCSharp. JavaScript (Jint, bounded) stays enabled by default.
/// </summary>
public sealed class RepositoryServiceScriptGateTests
{
    private const string CSharpScriptBpmn = """
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
          <process id="cs">
            <startEvent id="s"/>
            <scriptTask id="st" scriptFormat="C#" resultVariable="sum">
              <script><![CDATA[return 21*2;]]></script>
            </scriptTask>
            <endEvent id="e"/>
            <sequenceFlow id="f1" sourceRef="s" targetRef="st"/>
            <sequenceFlow id="f2" sourceRef="st" targetRef="e"/>
          </process>
        </definitions>
        """;

    private const string JavaScriptScriptBpmn = """
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL">
          <process id="js">
            <startEvent id="s"/>
            <scriptTask id="st" scriptFormat="JavaScript" resultVariable="sum">
              <script><![CDATA[return 21*2;]]></script>
            </scriptTask>
            <endEvent id="e"/>
            <sequenceFlow id="f1" sourceRef="s" targetRef="st"/>
            <sequenceFlow id="f2" sourceRef="st" targetRef="e"/>
          </process>
        </definitions>
        """;

    private static RepositoryService CreateService(bool allowCSharp)
    {
        var repo = new Mock<IProcessDefinitionRepository>();
        repo.Setup(r => r.GetLatestByKeyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<VertexBPMN.Domain.Entities.ProcessDefinition?>(null));
        repo.Setup(r => r.AddAsync(It.IsAny<VertexBPMN.Domain.Entities.ProcessDefinition>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Runtime:Scripts:AllowCSharp"] = allowCSharp ? "true" : "false"
            })
            .Build();
        return new RepositoryService(repo.Object, new BpmnParser(), config);
    }

    [Fact]
    public async Task CSharpScriptTask_IsRejected_WhenAllowCSharpIsFalse()
    {
        var service = CreateService(allowCSharp: false);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeployAsync(CSharpScriptBpmn, "cs-1", null, TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task CSharpScriptTask_IsAllowed_WhenAllowCSharpIsTrue()
    {
        var service = CreateService(allowCSharp: true);
        var def = await service.DeployAsync(CSharpScriptBpmn, "cs-2", null, TestContext.Current.CancellationToken);
        Assert.NotNull(def);
    }

    [Fact]
    public async Task JavaScriptScriptTask_IsAllowedByDefault()
    {
        var service = CreateService(allowCSharp: false);
        var def = await service.DeployAsync(JavaScriptScriptBpmn, "js-1", null, TestContext.Current.CancellationToken);
        Assert.NotNull(def);
    }
}
