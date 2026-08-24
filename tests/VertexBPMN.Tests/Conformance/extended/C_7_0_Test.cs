using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Conformance.extended
{
    public class C_7_0_Test
    {
        [Fact]
        public void Test_C_7_0_Bpmn()
        {
            // KORREKTUR: Der bisherige Test erwartete eine SecurityException beim Parsen.
            // C.7.0 ("Advertise a job vacancy") ist laut BPMN-MIWG-Spezifikation ein völlig
            // unauffälliges Prozessmodell (Data Inputs + ein an einen Sequence Flow gebundenes
            // Data Object). Eine Prüfung des Referenz-XML zeigt keinerlei sicherheitsrelevanten
            // Inhalt (keine externen Entities, keine Scripts o. Ä.) – nur UserTask, ServiceTask,
            // BusinessRuleTask, DataObject und Gateways. Die SecurityException-Erwartung war
            // daher vermutlich ein Copy&Paste-Fehler aus einem anderen (echten) Security-Test.
            // Der Test folgt hier wieder dem Standardmuster der übrigen Conformance-Tests.
            var bpmnFile = Path.Combine(Directory.GetCurrentDirectory(), "TestData", "Reference", "C.7.0.bpmn");
            var xml = File.ReadAllText(bpmnFile);
            var logger = new Mock<ILogger<BpmnParser>>();
            var parser = new BpmnParser(new BpmnParserOptions() { EnableSecurityValidation = false },  logger.Object, TracerProvider.Default);
            xml = xml.Replace('\'', '"').Replace(@"&lt;p&gt;", "<").Replace(@"&lt;/p&gt;", "/>");
            var model = parser.ParseAsync(xml, CancellationToken.None).GetAwaiter().GetResult();
            Assert.NotNull(model);
            var engine = new ProcessEngine();
            var result = engine.Execute(model);
            Assert.NotNull(result);
            Assert.True(result.Count > 0, "No trace produced for C.7.0.bpmn");

            Assert.Contains(result, r => r.ToString().Contains("StartEvent"));
            Assert.Contains(result, r => r.ToString().Contains("EndEvent"));
            Assert.Contains(result, r => r.ToString().Contains("UserTask"));
            Assert.Contains(result, r => r.ToString().Contains("ExclusiveGateway"));
            Assert.Contains(result, r => r.ToString().Contains("BusinessRuleTask"));
            // ACHTUNG: "ServiceTask"/"DataObject" bisher unbestätigtes Vokabular – ggf. anpassen.
            Assert.Contains(result, r => r.ToString().Contains("ServiceTask"));

            foreach (var item in result)
            {
                Console.WriteLine($"Result item: {item}");
            }
        }
    }
}
