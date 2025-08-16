using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
using VertexBPMN.Core.Engine;
using VertexBPMN.Core.Extensions;
using VertexBPMN.Core.Services;
using Xunit;

namespace VertexBPMN.Tests.TestSuite2025
{
    public class C_9_0_Test
    {
        [Fact]
        public void Test_C_9_0_Bpmn()
        {
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.9.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var parser = new BpmnParser();
            var model = parser.Parse(xml);
            Assert.NotNull(model);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddServiceTaskHandlers();
            var provider = services.BuildServiceProvider();
            var registry = provider.GetRequiredService<ServiceTaskRegistry>();

            // Act & Assert
            Assert.True(registry.TryResolve("calculateScore", out var calculateScoreHandler));
            Assert.NotNull(calculateScoreHandler);
            var engine = new TokenEngine(NullLogger<TokenEngine>.Instance, registry);
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.9.0.bpmn");
            // TODO: Add specific assertions for expected result
        }
    }
}
