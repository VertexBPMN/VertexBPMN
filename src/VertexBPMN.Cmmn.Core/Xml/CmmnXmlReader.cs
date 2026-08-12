using System.Xml.Linq;
using VertexBPMN.Domain.Model.Cmmn.CaseModel;
using VertexBPMN.Domain.Model.Cmmn.Common;
using VertexBPMN.Domain.Model.Cmmn.Core;
using VertexBPMN.Domain.Model.Cmmn.DI;
using VertexBPMN.Domain.Model.Cmmn.InformationModel;
using VertexBPMN.Domain.Model.Cmmn.PlanModel;

namespace VertexBPMN.Domain.Model.Cmmn.Xml;


public static class CmmnXmlReader
{
    public static Definitions Read(XDocument doc)
    {
        var root = doc.Root ?? throw new InvalidOperationException("Missing definitions");
        if (root.Name != "definitions".C()) throw new InvalidOperationException("Root must be cmmn:definitions");

        var defs = new Definitions { Id = root.Attr("id") ?? Guid.NewGuid().ToString("N"), TargetNamespace = root.Attr("targetNamespace") };

        foreach (var imp in root.Elements("import".C()))
            defs.Imports.Add(new Import(imp.Attr("importType"), new UriString(imp.Attr("locationURI")), imp.Attr("namespace")));

        // Build ID map for references
        var idMap = new Dictionary<string, CmmnElement>();

        // Types
        foreach (var t in root.Elements("caseFileItemDefinition".C()))
        {
            var type = new CaseFileItemDefinition { Id = t.Attr("id") ?? Guid.NewGuid().ToString("N"), Name = t.Attr("name"), StructureRef = t.Attr("structureRef") };
            defs.CaseFileItemDefinitions.Add(type);
            idMap[type.Id] = type;
        }

        // Cases
        foreach (var c in root.Elements("case".C()))
        {
            var cc = new Case { Id = c.Attr("id") ?? Guid.NewGuid().ToString("N"), Name = c.Attr("name") };

            // CaseFile
            var cfModel = c.Element("caseFileModel".C());
            if (cfModel is not null)
            {
                var cfEl = cfModel.Element("caseFile".C());
                if (cfEl is not null)
                {
                    var cf = new CaseFile { Id = cfEl.Attr("id") ?? Guid.NewGuid().ToString("N") };
                    foreach (var it in cfEl.Elements("caseFileItem".C()))
                        cf.RootItems.Add(ReadCaseFileItem(it, idMap));
                    cc.CaseFileModel = cf;
                }
            }

            // CasePlanModel
            var cpmEl = c.Element("casePlanModel".C());
            if (cpmEl is not null)
                cc.CasePlanModel = (CasePlanModel)ReadStage(cpmEl, idMap, asCasePlan: true);

            defs.Cases.Add(cc);
            idMap[cc.Id] = cc;
        }

        // DI
        var di = root.Element("CMMNDI".Cdi());
        if (di is not null)
            defs.CmmnDi = ReadDi(di, idMap);

        return defs;
    }

    static CaseFileItem ReadCaseFileItem(XElement el, Dictionary<string, CmmnElement> idMap)
    {
        var it = new CaseFileItem { Id = el.Attr("id") ?? Guid.NewGuid().ToString("N"), Name = el.Attr("name") };
        var dref = el.Attr("definitionRef");
        if (dref is not null && idMap.TryGetValue(dref, out var d) && d is CaseFileItemDefinition def) it.Definition = def;
        foreach (var ch in el.Elements("caseFileItem".C()))
        {
            var c = ReadCaseFileItem(ch, idMap); it.Children.Add(c);
        }
        idMap[it.Id] = it;
        return it;
    }

    static Stage ReadStage(XElement el, Dictionary<string, CmmnElement> idMap, bool asCasePlan = false)
    {
        Stage stg = asCasePlan ? new CasePlanModel() : new Stage();
        stg.Id = el.Attr("id") ?? Guid.NewGuid().ToString("N");
        stg.Name = el.Attr("name");

        foreach (var piEl in el.Elements("planItem".C()))
        {
            var pi = new PlanItem { Id = piEl.Attr("id") ?? Guid.NewGuid().ToString("N"), Name = piEl.Attr("name") };

            // Inline definition
            var defEl = piEl.Elements().FirstOrDefault(e => e.Name != "entryCriterion".C() && e.Name != "exitCriterion".C() && e.Name != "planItemControl".C());
            if (defEl is not null) pi.DefinitionRef = ReadDefinition(defEl, idMap);

            foreach (var ec in piEl.Elements("entryCriterion".C()))
                pi.EntryCriteria.Add((EntryCriterion)ReadCriterion(ec, idMap));
            foreach (var xc in piEl.Elements("exitCriterion".C()))
                pi.ExitCriteria.Add((ExitCriterion)ReadCriterion(xc, idMap));

            var ic = piEl.Element("planItemControl".C());
            if (ic is not null) pi.ItemControl = ReadItemControl(ic);

            stg.PlanItems.Add(pi);
            idMap[pi.Id] = pi;
        }

        idMap[stg.Id] = stg;
        return stg;
    }

    static PlanItemDefinition ReadDefinition(XElement el, Dictionary<string, CmmnElement> idMap)
    {
        PlanItemDefinition def = el.Name.LocalName switch
        {
            "humanTask" => new HumanTask(),
            "processTask" => new ProcessTask
            {
                // If you want to set ProcessRef by ID, you need to resolve it from idMap (if present)
                ProcessRef = el.Attr("processRef") is string procRefId && idMap.TryGetValue(procRefId, out var procObj) && procObj is Process proc
                    ? proc
                    : null
            },
            "caseTask" => new CaseTask
            {
                CaseRef = el.Attr("caseRef") is string caseRefId && idMap.TryGetValue(caseRefId, out var caseObj) && caseObj is Case c
                    ? c
                    : null
            },
            "decisionTask" => new DecisionTask
            {
                DecisionRef = el.Attr("decisionRef") is string decisionRefId && idMap.TryGetValue(decisionRefId, out var decObj) && decObj is Decision dec
                    ? dec
                    : null
            },
            "stage" => ReadStage(el, idMap),
            "timerEventListener" => new TimerEventListener
            {
                StartTrigger = el.Element("timerStart".C()) is XElement ts
                    ? new StartTrigger { TimerExpression = ts.Element("timerExpression".C()) is XElement te ? new Expression(null, te.Value) : null }
                    : null
            },
            "userEventListener" => new UserEventListener(),
            "planFragment" => new PlanFragment(),
            _ => new PlanFragment()
        };
        def.Id = el.Attr("id") ?? Guid.NewGuid().ToString("N");
        def.Name = el.Attr("name");
        idMap[def.Id] = def;
        return def;
    }

    static ItemControl ReadItemControl(XElement el)
    {
        var ic = new ItemControl { Id = el.Attr("id") ?? Guid.NewGuid().ToString("N") };
        var man = el.Element("manualActivationRule".C())?.Element("condition".C());
        if (man is not null) ic.ManualActivationRule = new ManualActivationRule { Condition = new Expression(null, man.Value) };
        var req = el.Element("requiredRule".C())?.Element("condition".C());
        if (req is not null) ic.RequiredRule = new RequiredRule { Condition = new Expression(null, req.Value) };
        var rep = el.Element("repetitionRule".C())?.Element("condition".C());
        if (rep is not null) ic.RepetitionRule = new RepetitionRule { Condition = new Expression(null, rep.Value) };
        return ic;
    }

    static Criterion ReadCriterion(XElement el, Dictionary<string, CmmnElement> idMap)
    {
        Criterion c;
        if (el.Name.LocalName == "entryCriterion")
            c = new EntryCriterion();
        else
            c = new ExitCriterion();
        c.Id = el.Attr("id") ?? Guid.NewGuid().ToString("N");
        var sEl = el.Element("sentry".C());
        if (sEl is not null) c.SentryRef = ReadSentry(sEl, idMap);
        return c;
    }

    static Sentry ReadSentry(XElement el, Dictionary<string, CmmnElement> idMap)
    {
        var s = new Sentry { Id = el.Attr("id") ?? Guid.NewGuid().ToString("N") };
        var ifp = el.Element("ifPart".C())?.Element("condition".C());
        if (ifp is not null) s.IfPart = new IfPart { Id = Guid.NewGuid().ToString("N"), Condition = new Expression(null, ifp.Value) };
        foreach (var op in el.Elements())
        {
            if (op.Name == "planItemOnPart".C())
            {
                var pop = new PlanItemOnPart { Id = op.Attr("id") ?? Guid.NewGuid().ToString("N"), StandardEvent = op.Attr("standardEvent") };
                var sref = op.Attr("sourceRef");
                if (sref is not null && idMap.TryGetValue(sref, out var elRef) && elRef is PlanItem pi) pop.SourceRef = pi;
                s.OnParts.Add(pop);
            }
            else if (op.Name == "caseFileItemOnPart".C())
            {
                var cop = new CaseFileItemOnPart { Id = op.Attr("id") ?? Guid.NewGuid().ToString("N"), Transition = op.Attr("standardEvent") };
                s.OnParts.Add(cop);
            }
        }
        idMap[s.Id] = s;
        return s;
    }

    static CmmnDi ReadDi(XElement di, Dictionary<string, CmmnElement> idMap)
    {
        var root = new CmmnDi();
        foreach (var d in di.Elements("CMMNDiagram".Cdi()))
        {
            var dia = new CmmnDiagram { Name = d.Attr("name") };
            foreach (var e in d.Elements())
            {
                if (e.Name == "CMMNShape".Cdi())
                {
                    var s = new CmmnShape { Bounds = ReadBounds(e.Element("Bounds".Dc())!) };
                    var cref = e.Attr("cmmnElementRef");
                    if (cref is not null && idMap.TryGetValue(cref, out var be)) s.CmmnElementRef = be;
                    dia.DiagramElements.Add(s);
                }
                else if (e.Name == "CMMNEdge".Cdi())
                {
                    var ed = new CmmnEdge();
                    var cref = e.Attr("cmmnElementRef");
                    if (cref is not null && idMap.TryGetValue(cref, out var be)) ed.CmmnElementRef = be;
                    foreach (var wp in e.Elements("waypoint".Di()))
                        ed.Waypoints.Add(new Point(wp.AttrDouble("x") ?? 0, wp.AttrDouble("y") ?? 0));
                    dia.DiagramElements.Add(ed);
                }
            }
            root.Diagrams.Add(dia);
        }
        return root;
    }

    static Bounds ReadBounds(XElement b)
        => new Bounds(b.AttrDouble("x") ?? 0, b.AttrDouble("y") ?? 0, b.AttrDouble("width") ?? 0, b.AttrDouble("height") ?? 0);
}