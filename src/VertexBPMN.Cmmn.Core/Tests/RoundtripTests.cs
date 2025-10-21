using VertexBPMN.Domain.Model.Cmmn.CaseModel;
using VertexBPMN.Domain.Model.Cmmn.Common;
using VertexBPMN.Domain.Model.Cmmn.Core;
using VertexBPMN.Domain.Model.Cmmn.DI;
using VertexBPMN.Domain.Model.Cmmn.InformationModel;
using VertexBPMN.Domain.Model.Cmmn.PlanModel;
using VertexBPMN.Domain.Model.Cmmn.Xml;
using Xunit;

namespace VertexBPMN.Domain.Model.Cmmn.Tests;


public class RoundtripTests
{
    [Fact]
    public void Case_With_Stage_Task_Sentry_Roundtrips()
    {
        var defs = new Definitions { Id = "defs1", TargetNamespace = "http://example.com/cmmn" };

        var c = new Case { Id = "case1", Name = "Simple" };
        c.CaseFileModel = new CaseFile { Id = "cf1" };
        var customer = new CaseFileItemDefinition { Id = "tCustomer", Name = "Customer", StructureRef = "{http://ex}tCustomer" };
        defs.CaseFileItemDefinitions.Add(customer);
        var cfi = new CaseFileItem { Id = "c1", Name = "customer", Definition = customer };
        c.CaseFileModel.RootItems.Add(cfi);

        var root = new CasePlanModel { Id = "cpm1", Name = "Root" };
        var htDef = new HumanTask { Id = "ht1", Name = "Approve" };
        var ht = new PlanItem { Id = "pi1", Name = "PI Approve", DefinitionRef = htDef };

        var stage = new Stage { Id = "stg1", Name = "Stage A" };
        var s = new Sentry { Id = "s1", IfPart = new IfPart { Id = "if1", Condition = new Expression(null, "approved = true") } };
        var ec = new EntryCriterion { Id = "ec1", SentryRef = s };
        ht.EntryCriteria.Add(ec);
        stage.PlanItems.Add(ht);
        root.PlanItems.Add(new PlanItem { Id = "piStage", Name = "Substage", DefinitionRef = stage });

        c.CasePlanModel = root;
        defs.Cases.Add(c);

        defs.CmmnDi = new CmmnDi();
        var d = new CmmnDiagram { Name = "D1" };
        d.DiagramElements.Add(new CmmnShape { Bounds = new Bounds(10, 10, 80, 40), CmmnElementRef = ht });
        defs.CmmnDi.Diagrams.Add(d);

        var xml = CmmnXmlWriter.Write(defs);
        var defs2 = CmmnXmlReader.Read(xml);

        Assert.NotNull(defs2);
        Assert.Single(defs2.Cases);
        Assert.Equal("Simple", defs2.Cases[0].Name);
        Assert.NotNull(defs2.Cases[0].CasePlanModel);
        Assert.NotEmpty(defs2.Cases[0].CasePlanModel!.PlanItems);
    }
}
