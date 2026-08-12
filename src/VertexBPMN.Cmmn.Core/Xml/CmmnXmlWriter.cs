using System.Xml.Linq;
using VertexBPMN.Domain.Model.Cmmn.CaseModel;
using VertexBPMN.Domain.Model.Cmmn.Core;
using VertexBPMN.Domain.Model.Cmmn.DI;
using VertexBPMN.Domain.Model.Cmmn.InformationModel;
using VertexBPMN.Domain.Model.Cmmn.PlanModel;

namespace VertexBPMN.Domain.Model.Cmmn.Xml;

public static class CmmnXmlWriter
{
    public static XDocument Write(Definitions defs)
    {
        var root = new XElement("definitions".C(),
            new XAttribute("id", defs.Id),
            defs.TargetNamespace is null ? null : new XAttribute("targetNamespace", defs.TargetNamespace),
            new XAttribute(XNamespace.Xmlns + "cmmn", Ns.CMMN.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "cmmndi", Ns.CMMNDI.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "di", Ns.DI.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "dc", Ns.DC.NamespaceName));

        foreach (var imp in defs.Imports)
            root.Add(new XElement("import".C(),
                imp.ImportType is null ? null : new XAttribute("importType", imp.ImportType),
                imp.Location is null ? null : new XAttribute("locationURI", imp.Location),
                imp.Namespace is null ? null : new XAttribute("namespace", imp.Namespace)));

        foreach (var type in defs.CaseFileItemDefinitions)
            root.Add(new XElement("caseFileItemDefinition".C(),
                new XAttribute("id", type.Id),
                type.Name is null ? null : new XAttribute("name", type.Name),
                type.StructureRef is null ? null : new XAttribute("structureRef", type.StructureRef)));

        foreach (var c in defs.Cases)
            root.Add(WriteCase(c));

        // DI
        if (defs.CmmnDi is not null)
            root.Add(WriteDi(defs.CmmnDi));

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
    }

    static XElement WriteCase(Case c)
    {
        var xe = new XElement("case".C(),
            new XAttribute("id", c.Id),
            c.Name is null ? null : new XAttribute("name", c.Name));

        if (c.CaseFileModel is not null)
        {
            var cf = new XElement("caseFileModel".C());
            var cfRoot = new XElement("caseFile".C(), new XAttribute("id", c.CaseFileModel.Id));
            foreach (var it in c.CaseFileModel.RootItems)
                cfRoot.Add(WriteCaseFileItem(it));
            cf.Add(cfRoot);
            xe.Add(cf);
        }

        if (c.CasePlanModel is not null)
            xe.Add(WriteStage("casePlanModel", c.CasePlanModel));

        return xe;
    }

    static XElement WriteCaseFileItem(CaseFileItem it)
    {
        var x = new XElement("caseFileItem".C(),
            new XAttribute("id", it.Id),
            it.Name is null ? null : new XAttribute("name", it.Name),
            it.Definition?.Id is null ? null : new XAttribute("definitionRef", it.Definition.Id));
        foreach (var ch in it.Children) x.Add(WriteCaseFileItem(ch));
        return x;
    }

    static XElement WriteStage(string elementName, Stage stg)
    {
        var x = new XElement(elementName.C(),
            new XAttribute("id", stg.Id),
            stg.Name is null ? null : new XAttribute("name", stg.Name));

        // Definitions for Tasks / Fragments (as siblings in CMMN, but we inline minimal approach)
        // PlanItems
        foreach (var pi in stg.PlanItems)
            x.Add(WritePlanItem(pi));

        return x;
    }

    static XElement WritePlanItem(PlanItem pi)
    {
        var x = new XElement("planItem".C(),
            new XAttribute("id", pi.Id),
            pi.Name is null ? null : new XAttribute("name", pi.Name));

        if (pi.DefinitionRef is not null)
        {
            // write definition as nested element for simplicity
            x.Add(WriteDefinition(pi.DefinitionRef));
        }

        if (pi.ItemControl is not null) x.Add(WriteItemControl(pi.ItemControl));
        foreach (var e in pi.EntryCriteria) x.Add(WriteCriterion("entryCriterion", e));
        foreach (var e in pi.ExitCriteria) x.Add(WriteCriterion("exitCriterion", e));
        return x;
    }

    static XElement WriteDefinition(PlanItemDefinition def)
        => def switch
        {
            HumanTask ht => new XElement("humanTask".C(),
                new XAttribute("id", ht.Id),
                ht.Name is null ? null : new XAttribute("name", ht.Name),
                new XAttribute("isBlocking", ht.IsBlocking)),
            ProcessTask pt => new XElement("processTask".C(),
                new XAttribute("id", pt.Id),
                pt.Name is null ? null : new XAttribute("name", pt.Name),
                new XAttribute("isBlocking", pt.IsBlocking),
                pt.ProcessRef is null ? null : new XAttribute("processRef", pt.ProcessRef)),
            CaseTask ct => new XElement("caseTask".C(),
                new XAttribute("id", ct.Id),
                ct.Name is null ? null : new XAttribute("name", ct.Name),
                new XAttribute("isBlocking", ct.IsBlocking),
                ct.CaseRef is null ? null : new XAttribute("caseRef", ct.CaseRef)),
            DecisionTask dt => new XElement("decisionTask".C(),
                new XAttribute("id", dt.Id),
                dt.Name is null ? null : new XAttribute("name", dt.Name),
                new XAttribute("isBlocking", dt.IsBlocking),
                dt.DecisionRef is null ? null : new XAttribute("decisionRef", dt.DecisionRef)),
            Stage stg => WriteStage("stage", stg),
            TimerEventListener tel => new XElement("timerEventListener".C(),
                new XAttribute("id", tel.Id),
                tel.StartTrigger is null ? null : new XElement("timerStart".C(),
                    tel.StartTrigger.TimerExpression is null ? null : new XElement("timerExpression".C(), tel.StartTrigger.TimerExpression.Body))),
            UserEventListener uel => new XElement("userEventListener".C(), new XAttribute("id", uel.Id)),
            PlanFragment pf => new XElement("planFragment".C(), new XAttribute("id", pf.Id)),
            _ => new XElement("planItemDefinition".C(), new XAttribute("id", def.Id))
        };

    static XElement WriteItemControl(PlanItemControl ic)
    {
        var x = new XElement("planItemControl".C(), new XAttribute("id", ic.Id));
        if (ic.ManualActivationRule?.Condition is not null)
            x.Add(new XElement("manualActivationRule".C(), new XElement("condition".C(), ic.ManualActivationRule.Condition.Body)));
        if (ic.RequiredRule?.Condition is not null)
            x.Add(new XElement("requiredRule".C(), new XElement("condition".C(), ic.RequiredRule.Condition.Body)));
        if (ic.RepetitionRule?.Condition is not null)
            x.Add(new XElement("repetitionRule".C(), new XElement("condition".C(), ic.RepetitionRule.Condition.Body)));
        return x;
    }

    static XElement WriteCriterion(string localName, Criterion c)
    {
        var x = new XElement(localName.C(), new XAttribute("id", c.Id));
        if (c.SentryRef is not null)
            x.Add(WriteSentry(c.SentryRef));
        return x;
    }

    static XElement WriteSentry(Sentry s)
    {
        var x = new XElement("sentry".C(), new XAttribute("id", s.Id));
        if (s.IfPart?.Condition is not null)
            x.Add(new XElement("ifPart".C(), new XElement("condition".C(), s.IfPart.Condition.Body)));
        foreach (var op in s.OnParts)
        {
            if (op is PlanItemOnPart pop)
            {
                var xe = new XElement("planItemOnPart".C(),
                    pop.SourceRef is not null ? new XAttribute("sourceRef", pop.SourceRef.Id) : null,
                    pop.StandardEvent is not null ? new XAttribute("standardEvent", pop.StandardEvent) : null);
                x.Add(xe);
            }
            else if (op is CaseFileItemOnPart cop)
            {
                var xe = new XElement("caseFileItemOnPart".C(),
                    cop.SourceRef is not null ? new XAttribute("sourceRef", cop.SourceRef.Id) : null,
                    cop.Transition is not null ? new XAttribute("standardEvent", cop.Transition) : null);
                x.Add(xe);
            }
        }
        return x;
    }

    static XElement WriteDi(CmmnDi diRoot)
    {
        var xdi = new XElement("CMMNDI".Cdi());
        foreach (var d in diRoot.Diagrams)
        {
            var xd = new XElement("CMMNDiagram".Cdi(),
                d.Name is null ? null : new XAttribute("name", d.Name));
            foreach (var e in d.DiagramElements)
            {
                if (e is CmmnShape s)
                {
                    var xs = new XElement("CMMNShape".Cdi(),
                        s.CmmnElementRef is null ? null : new XAttribute("cmmnElementRef", s.CmmnElementRef.Id));
                    xs.Add(new XElement("Bounds".Dc(),
                        new XAttribute("x", s.Bounds.X), new XAttribute("y", s.Bounds.Y),
                        new XAttribute("width", s.Bounds.Width), new XAttribute("height", s.Bounds.Height)));
                    xd.Add(xs);
                }
                else if (e is CmmnEdge ed)
                {
                    var xe = new XElement("CMMNEdge".Cdi(),
                        ed.CmmnElementRef is null ? null : new XAttribute("cmmnElementRef", ed.CmmnElementRef.Id));
                    foreach (var p in ed.Waypoints)
                        xe.Add(new XElement("waypoint".Di(), new XAttribute("x", p.X), new XAttribute("y", p.Y)));
                    xd.Add(xe);
                }
            }
            xdi.Add(xd);
        }
        return xdi;
    }
}