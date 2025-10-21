using VertexBPMN.Domain.Model.Dmn.Core;
using VertexBPMN.Domain.Model.Dmn.DecisionTable;
using VertexBPMN.Domain.Model.Dmn.DI;
using VertexBPMN.Domain.Model.Dmn.DRD;
using VertexBPMN.Domain.Model.Dmn.Expressions;
using VertexBPMN.Domain.Model.Dmn.Xml;
using Xunit;

namespace VertexBPMN.Domain.Model.Dmn.Tests;

public class RoundtripTests
{
    [Fact]
    public void DecisionTable_Roundtrip_Works()
    {
        var defs = new Definitions { Id = "loan", Name = "Loan", NamespaceUri = "http://example.com/dmn" };

        var inData = new InputData { Id = "ApplicantIncome", Name = "ApplicantIncome", Variable = new InformationItem { Id = "varIncome", Name = "income", TypeRef = "number" } };
        defs.DrgElements.Add(inData);

        var decision = new Decision { Id = "Eligibility", Name = "Eligibility", Variable = new InformationItem { Id = "varOut", Name = "eligibility", TypeRef = "string" } };
        var table = new DecisionTable.DecisionTable { HitPolicy = HitPolicy.UNIQUE, PreferredOrientation = DecisionTableOrientation.RuleAsRow, OutputLabel = "eligibility" };

        table.Inputs.Add(new InputClause
        {
            InputExpression = new LiteralExpression
            {
                Text = "income",
                ExpressionLanguage = new Uri("feel", UriKind.Relative)
            },
            InputValues = new UnaryTests { Text = "[0..100000]" } });
        table.Outputs.Add(new OutputClause { Name = "eligibility", TypeRef = "string", OutputValues = new UnaryTests { Text = "\"yes\",\"no\"" } });

        var rule1 = new DecisionRule(); rule1.InputEntry.Add(new UnaryTests { Text = ">= 50000" }); rule1.OutputEntry.Add(new LiteralExpression { Text = "\"yes\"" }); table.Rules.Add(rule1);
        var rule2 = new DecisionRule(); rule2.InputEntry.Add(new UnaryTests { Text = "< 50000" }); rule2.OutputEntry.Add(new LiteralExpression { Text = "\"no\"" }); table.Rules.Add(rule2);

        decision.DecisionLogic = table;
        decision.InformationRequirements.Add(new Requirements.InformationRequirement { RequiredInput = inData });
        defs.DrgElements.Add(decision);

        defs.DmnDi = new DMNDI();
        var diagram = new DMNDiagram { Name = "Main" };
        diagram.Elements.Add(new DMNShape { DmnElementRef = decision, Bounds = new Bounds { X = 20, Y = 30, Width = 180, Height = 80 } });
        defs.DmnDi.Diagrams.Add(diagram);

        var xml = DmnXmlWriter.Write(defs);
        var defs2 = DmnXmlReader.Read(xml);

        Assert.Equal(defs.NamespaceUri, defs2.NamespaceUri);
        Assert.Contains(defs2.DrgElements, e => e is Decision);
        Assert.Contains(defs2.DrgElements, e => e is InputData);
        var d2 = (Decision)Assert.Single(defs2.DrgElements, e => e is Decision);
        Assert.Equal("Eligibility", d2.Name);
        var dt2 = Assert.IsType<DecisionTable.DecisionTable>(d2.DecisionLogic!);
        Assert.Equal(2, dt2.Rules.Count);
    }
}