//using Microsoft.Extensions.Logging;
//using Moq;
//using VertexBPMN.Domain.Model.Dmn;
//using VertexBPMN.Domain.Model;

//using Xunit;

//namespace VertexBPMN.Test.Parsing.Conformance
//{
//    public class C_8_1_Test
//    {
//        [Fact]
//        public void Test_C_8_1_Bpmn()
//        {
//            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.8.1.bpmn");
//            var xml = File.ReadAllText(bpmnFile);
//            var mockDecision = new Mock<IDecisionService>();
//            mockDecision.Setup(d => d.EvaluateDecisionByKeyAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>>(),
//                It.IsAny<string>(), It.IsAny<CancellationToken>()))
//                .ReturnsAsync(new DecisionResult(new Dictionary<string, object> { ["Approval"] = "Manual Validation Required" }));
            
//            var parser = new BpmnParser();
//            var model =  parser.ParseAsync(xml.Replace('\'', '"')).GetAwaiter().GetResult();
//            Assert.NotNull(model);
//            var engine = new ProcessEngine(Mock.Of<ILogger<ProcessEngine>>(),
//                NullServiceTaskRegistry.Instance, mockDecision.Object);
           
//            var result = engine.Execute(model);
//            Assert.NotNull(result);
//            Assert.True(result.Count > 0, "No trace produced for C.8.1.bpmn");
//                var types = result.Select(x => x.Split(':')[0].Trim()).ToList();
//                Assert.Contains("StartEvent", types);
//                Assert.DoesNotContain("UserTask", types); 
//                Assert.Contains("BusinessRuleTask", types);  // Verify the rule eval happens
//                Assert.Contains("SequenceFlow", types);
//                Assert.Contains("ExclusiveGateway", types);
//                Assert.Contains("EndEvent", types);
//                foreach (var item in result)
//                {
//                    Console.WriteLine($"Result item: {item}");
//                }
//        }
//    }
//}
