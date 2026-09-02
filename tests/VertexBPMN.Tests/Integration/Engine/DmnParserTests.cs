using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Moq;

using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Integration.Engine;

public class DmnParserTests
{
    private readonly Mock<ILogger<DmnParser>> _loggerMock;
    private readonly DmnParser _parser;

    public DmnParserTests()
    {
        _loggerMock = new Mock<ILogger<DmnParser>>();
        _parser = new DmnParser(_loggerMock.Object);
    }

    [Fact]
    public async Task ParseAsync_ValidDecisionTableWithHitPolicy_ReturnsDmnDecision()
    {

        var dmnXml = @"<?xml version='1.0' encoding='UTF-8'?>
                                <definitions xmlns='https://www.omg.org/spec/DMN/20191111/MODEL/'>
                                    <decision id='creditDecision' name='Credit Approval'>
                                        <decisionTable id='table1' hitPolicy='FIRST'>
                                            <input id='input1' label='Age' typeRef='integer'/>
                                            <input id='input2' label='Income' typeRef='integer'/>
                                            <output id='output1' label='Approval' typeRef='string'/>
                                            <rule id='rule1'>
                                                <inputEntry id='input1'><text><![CDATA[>=18]]></text></inputEntry>
                                                <inputEntry id='input2'><text><![CDATA[=>30000]]></text></inputEntry>
                                                <outputEntry id='output1'><text><![CDATA[Approved]]></text></outputEntry>
                                            </rule>
                                            <rule id='rule2'>
                                                <inputEntry id='input1'><text><![CDATA[>=18]]></text></inputEntry>
                                                <inputEntry id='input2'><text><![CDATA[<=30000]]></text></inputEntry>
                                                <outputEntry id='output1'><text><![CDATA[Denied]]></text></outputEntry>
                                            </rule>
                                        </decisionTable>
                                    </decision>
                                </definitions>";

        var decision = await _parser.ParseAsync(dmnXml, TestContext.Current.CancellationToken);

        Assert.Equal("creditDecision", decision.Id);
        Assert.Equal("FIRST", decision.HitPolicy);
        Assert.Equal(2, decision.Inputs.Count);
        Assert.Equal("Age", decision.Inputs[0].Label);
        Assert.Equal("Income", decision.Inputs[1].Label);

        // Updated to satisfy xUnit2013 (Assert.Single instead of Count comparison)
        var output = Assert.Single(decision.Outputs);
        Assert.Equal("Approval", output.Label);

        Assert.Equal(2, decision.Rules.Count);
        Assert.Contains(decision.Rules, r => r.InputConditions["input1"] == ">=18" && r.OutputValues["output1"].ToString() == "Approved");
    }

    [Fact]
    public async Task ParseAsync_ValidDecisionTableWithHitPolicy_ReturnsDmnDecision2()
    {
        var path = Path.Combine("TestData", "dinnerDecisions.dmn");
        var dmnXml = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        var decision = await _parser.ParseAsync(dmnXml, TestContext.Current.CancellationToken);

        Assert.NotNull(decision);

        var xdoc = XDocument.Parse(dmnXml);

        // Determine namespace (support default or prefixed)
        XNamespace modelNs = xdoc.Root!.Name.Namespace;

        // Locate first decision
        var decisionElement = xdoc.Descendants(modelNs + "decision").FirstOrDefault();
        Assert.NotNull(decisionElement);

        var expectedDecisionId = (string?)decisionElement!.Attribute("id");
        Assert.False(string.IsNullOrWhiteSpace(expectedDecisionId));
        Assert.Equal(expectedDecisionId, decision.Id);

        var decisionTable = decisionElement.Element(modelNs + "decisionTable");
        Assert.NotNull(decisionTable);

        var expectedHitPolicy = (string?)decisionTable!.Attribute("hitPolicy");
        if (!string.IsNullOrWhiteSpace(expectedHitPolicy))
            Assert.Equal(expectedHitPolicy, decision.HitPolicy);
        else
            Assert.False(string.IsNullOrWhiteSpace(decision.HitPolicy)); // Parser should set something sensible

        // Inputs
        var expectedInputElements = decisionTable.Elements(modelNs + "input").ToList();
        Assert.NotEmpty(expectedInputElements);
        Assert.Equal(expectedInputElements.Count, decision.Inputs.Count);
        foreach (var inputEl in expectedInputElements)
        {
            var inputId = (string?)inputEl.Attribute("id");
            Assert.Contains(decision.Inputs, i => i.Id == inputId);
        }

        // Outputs
        var expectedOutputElements = decisionTable.Elements(modelNs + "output").ToList();
        Assert.NotEmpty(expectedOutputElements);
        Assert.Equal(expectedOutputElements.Count, decision.Outputs.Count);
        foreach (var outputEl in expectedOutputElements)
        {
            var outputId = (string?)outputEl.Attribute("id");
            Assert.Contains(decision.Outputs, o => o.Id == outputId);
        }

        // Rules
        var expectedRuleElements = decisionTable.Elements(modelNs + "rule").ToList();
        Assert.NotEmpty(expectedRuleElements);
        Assert.Equal(expectedRuleElements.Count, decision.Rules.Count);

        // Basic structural validation: each rule should have at least one output mapping
        foreach (var rule in decision.Rules)
        {
            Assert.NotEmpty(rule.OutputValues);
        }
    }

    [Fact]
    public async Task ParseAsync_InvalidHitPolicy_ThrowsException()
    {
        var dmnXml = @"
                        <dmn:definitions xmlns:dmn='https://www.omg.org/spec/DMN/20191111/MODEL/'>
                            <dmn:decision id='creditDecision'>
                                <dmn:decisionTable id='table1' hitPolicy='INVALID'>
                                    <dmn:input id='input1' label='Age' typeRef='integer'/>
                                    <dmn:output id='output1' label='Approval' typeRef='string'/>
                                    <dmn:rule id='rule1'>
                                        <dmn:inputEntry id='input1'><dmn:text>>=18</dmn:text></dmn:inputEntry>
                                        <dmn:outputEntry id='output1'><dmn:text>Approved</dmn:text></dmn:outputEntry>
                                    </dmn:rule>
                                </dmn:decisionTable>
                            </dmn:decision>
                        </dmn:definitions>";

        await Assert.ThrowsAsync<DmnParseException>(() => _parser.ParseAsync(dmnXml, TestContext.Current.CancellationToken));
    }
}