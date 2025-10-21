//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Logging.Abstractions;
//using Moq;
//using VertexBPMN.Domain.Interfaces;
//using VertexBPMN.Engine;
//using VertexBPMN.Engine.Parsing;
//using Xunit;

//namespace VertexBPMN.Test.Parsing.Conformance
//{
//    public class C_9_0_Test
//    {
//        [Fact]
//        public void Test_C_9_0_Bpmn()
//        {
//            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.9.0.bpmn");
//            var xml = File.ReadAllText(bpmnFile);
//            var logger = new Mock<ILogger<BpmnParser>>();
//            var parser = new BpmnParser();
//            var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();

//            Assert.NotNull(model);
//            var services = new ServiceCollection();
//            services.AddLogging();
//            //services.AddServiceTaskHandlers();
//            var provider = services.BuildServiceProvider();
//            var registry = provider.GetRequiredService<IServiceTaskRegistry>();

//            // Act & Assert
//            Assert.True(registry.TryResolve("calculateScore", out var calculateScoreHandler));
//            Assert.NotNull(calculateScoreHandler);
//            var engine = new FullConformanceProcessEngine(NullLogger<FullConformanceProcessEngine>.Instance, registry);
//            var result = engine.Execute(model);
//            Assert.NotNull(result);
//            Assert.True(result.Count > 0, "No trace produced for C.9.0.bpmn");
//            // TODO: Add specific assertions for expected result
//        }
//    }
//}
