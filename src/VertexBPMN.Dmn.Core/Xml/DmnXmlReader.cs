using System.Xml.Linq;
using VertexBPMN.Domain.Model.Dmn.Core;
using VertexBPMN.Domain.Model.Dmn.DecisionTable;
using VertexBPMN.Domain.Model.Dmn.DI;
using VertexBPMN.Domain.Model.Dmn.DRD;
using VertexBPMN.Domain.Model.Dmn.Expressions;
using VertexBPMN.Domain.Model.Dmn.Requirements;

namespace VertexBPMN.Domain.Model.Dmn.Xml;

public static class DmnXmlReader
{
    public static Definitions Read(XDocument doc)
    {
        var r = doc.Root ?? throw new InvalidOperationException("Missing definitions");
        if (r.Name != "definitions".N()) throw new InvalidOperationException("Root must be dmn:definitions");

        var defs = new Definitions { Id = r.A("id"), Name = r.A("name") ?? "", NamespaceUri = r.A("namespace") ?? "", ExpressionLanguage = new Uri(r.A("expressionLanguage") ?? ""), TypeLanguage = new Uri(r.A("typeLanguage") ?? "") };

        var idMap = new Dictionary<string, DMNElement>();

        foreach (var imp in r.Elements("import".N()))
            defs.Imports.Add(item: new Import { Id = imp.A("id"), Name = imp.A("name") ?? "", ImportType = new Uri (imp.A("importType") ?? ""), LocationURI = new Uri(imp.A("locationURI")), Namespace = new Uri(imp.A("namespace") ?? "") });

        foreach (var it in r.Elements("itemDefinition".N()))
        {
            var id = new ItemDefinition { Id = it.A("id"), Name = it.A("name") ?? "", TypeRef = it.A("typeRef") ?? "Any" };
            if (bool.TryParse(it.A("isCollection"), out var col)) id.IsCollection = col;
            var tc = it.Element("typeConstraint".N())?.Element("text".N());
            if (tc is not null) id.TypeConstraint = new UnaryTests { Text = tc.Value };
            defs.ItemDefinitions.Add(id);
            if (id.Id is not null) idMap[id.Id] = id;
        }

        foreach (var el in r.Elements())
        {
            if (el.Name == "decision".N())
            {
                var d = new Decision { Id = el.A("id"), Name = el.A("name") ?? "" };
                var varEl = el.Element("variable".N());
                if (varEl is not null) d.Variable = ReadInformationItem(varEl);

                foreach (var ir in el.Elements("informationRequirement".N()))
                {
                    var req = new InformationRequirement { Id = ir.A("id") };
                    var rd = ir.Element("requiredDecision".N())?.A("href")?.TrimStart('#');
                    var ri = ir.Element("requiredInput".N())?.A("href")?.TrimStart('#');
                    if (rd is not null) req.RequiredDecision = new Decision { Id = rd };
                    if (ri is not null) req.RequiredInput = new InputData { Id = ri };
                    d.InformationRequirements.Add(req);
                }
                foreach (var kr in el.Elements("knowledgeRequirement".N()))
                {
                    var href = kr.Element("requiredKnowledge".N())?.A("href")?.TrimStart('#');
                    var req = new KnowledgeRequirement { Id = kr.A("id") };
                    if (href is not null) req.RequiredKnowledge = new BusinessKnowledgeModel { Id = href };
                    d.KnowledgeRequirements.Add(req);
                }
                foreach (var ar in el.Elements("authorityRequirement".N()))
                {
                    var req = new AuthorityRequirement { Id = ar.A("id") };
                    var ra = ar.Element("requiredAuthority".N())?.A("href")?.TrimStart('#');
                    var rd = ar.Element("requiredDecision".N())?.A("href")?.TrimStart('#');
                    var ri = ar.Element("requiredInput".N())?.A("href")?.TrimStart('#');
                    if (ra is not null) req.RequiredAuthority = new KnowledgeSource { Id = ra };
                    if (rd is not null) req.RequiredDecision = new Decision { Id = rd };
                    if (ri is not null) req.RequiredInput = new InputData { Id = ri };
                    d.AuthorityRequirements.Add(req);
                }

                var logic = el.Element("decisionLogic".N());
                if (logic is not null) d.DecisionLogic = ReadExpression(logic.Elements().First());

                defs.DrgElements.Add(d);
                if (d.Id is not null) idMap[d.Id] = d;
            }
            else if (el.Name == "inputData".N())
            {
                var i = new InputData { Id = el.A("id"), Name = el.A("name") ?? "" };
                var varEl = el.Element("variable".N());
                if (varEl is not null) i.Variable = ReadInformationItem(varEl);
                defs.DrgElements.Add(i);
                if (i.Id is not null) idMap[i.Id] = i;
            }
            else if (el.Name == "businessKnowledgeModel".N())
            {
                var b = new BusinessKnowledgeModel { Id = el.A("id"), Name = el.A("name") ?? "" };
                var enc = el.Element("encapsulatedLogic".N())?.Elements().FirstOrDefault();
                if (enc is not null) b.EncapsulatedLogic = ReadExpression(enc) as FunctionDefinition;
                defs.DrgElements.Add(b);
                if (b.Id is not null) idMap[b.Id] = b;
            }
            else if (el.Name == "decisionService".N())
            {
                var ds = new DecisionService { Id = el.A("id"), Name = el.A("name") ?? "" };
                var varEl = el.Element("variable".N());
                if (varEl is not null) ds.Variable = ReadInformationItem(varEl);
                foreach (var od in el.Elements("outputDecision".N())) ds.OutputDecisions.Add(new Decision { Id = od.A("href")?.TrimStart('#') });
                foreach (var ed in el.Elements("encapsulatedDecision".N())) ds.EncapsulatedDecisions.Add(new Decision { Id = ed.A("href")?.TrimStart('#') });
                foreach (var id in el.Elements("inputDecision".N())) ds.InputDecisions.Add(new Decision { Id = id.A("href")?.TrimStart('#') });
                foreach (var ii in el.Elements("inputData".N())) ds.InputData.Add(new InputData { Id = ii.A("href")?.TrimStart('#') });
                defs.DrgElements.Add(ds);
                if (ds.Id is not null) idMap[ds.Id] = ds;
            }
            else if (el.Name == "knowledgeSource".N())
            {
                var ks = new KnowledgeSource { Id = el.A("id"), Name = el.A("name") ?? "" };
                defs.DrgElements.Add(ks);
                if (ks.Id is not null) idMap[ks.Id] = ks;
            }
        }

        var dmndi = r.Element("DMNDI".D());
        if (dmndi is not null) defs.DmnDi = ReadDi(dmndi, idMap);

        return defs;
    }

    static InformationItem ReadInformationItem(XElement el)
        => new InformationItem { Id = el.A("id"), Name = el.A("name") ?? "", TypeRef = el.A("typeRef") ?? "Any" };

    static Expression ReadExpression(XElement el) => el.Name.LocalName switch
    {
        "literalExpression" => new LiteralExpression { Id = el.A("id"), ExpressionLanguage = new Uri(el.A("expressionLanguage")), Text = el.Element("text".N())?.Value },
        "decisionTable" => ReadDecisionTable(el),
        "invocation" => ReadInvocation(el),
        "functionDefinition" => ReadFunctionDefinition(el),
        _ => new LiteralExpression { Text = el.Value }
    };

    static Invocation ReadInvocation(XElement el)
    {
        var inv = new Invocation { Id = el.A("id") };
        var expr = el.Element("expression".N())?.Elements().FirstOrDefault();
        if (expr is not null) inv.CalledFunction = ReadExpression(expr);
        foreach (var b in el.Elements("binding".N()))
        {
            var binding = new Binding { Parameter = new InformationItem { Name = b.Element("parameter".N())?.A("name") ?? "" } };
            var e = b.Element("expression".N())?.Elements().FirstOrDefault();
            if (e is not null) binding.BindingFormula = ReadExpression(e);
            inv.Bindings.Add(binding);
        }
        return inv;
    }

    static FunctionDefinition ReadFunctionDefinition(XElement el)
    {
        var fd = new FunctionDefinition { Id = el.A("id"), TypeRef = el.A("typeRef") };
        var kind = el.A("kind"); if (!string.IsNullOrEmpty(kind)) fd.Kind = kind!;
        foreach (var p in el.Elements("formalParameter".N())) fd.Parameters.Add(new InformationItem { Name = p.A("name") ?? "", TypeRef = p.A("typeRef") ?? "Any" });
        var body = el.Element("expression".N())?.Elements().FirstOrDefault();
        if (body is not null) fd.Body = ReadExpression(body);
        return fd;
    }

    static DecisionTable.DecisionTable ReadDecisionTable(XElement el)
    {
        var dt = new DecisionTable.DecisionTable { Id = el.A("id") };
        var hp = el.A("hitPolicy"); if (!string.IsNullOrEmpty(hp) && Enum.TryParse<HitPolicy>(hp, out var hpv)) dt.HitPolicy = hpv;
        var agg = el.A("aggregation"); if (!string.IsNullOrEmpty(agg) && Enum.TryParse<BuiltinAggregator>(agg, out var aggv)) dt.Aggregation = aggv;
        var po = el.A("preferredOrientation"); if (!string.IsNullOrEmpty(po) && Enum.TryParse<DecisionTableOrientation>(po, out var pov)) dt.PreferredOrientation = pov;
        dt.OutputLabel = el.A("outputLabel");

        foreach (var i in el.Elements("input".N()))
        {
            var ic = new InputClause();
            var ie = i.Element("inputExpression".N())?.Elements().FirstOrDefault();
            if (ie is not null) ic.InputExpression = ReadExpression(ie);
            var iv = i.Element("inputValues".N())?.Element("text".N());
            if (iv is not null) ic.InputValues = new UnaryTests { Text = iv.Value };
            dt.Inputs.Add(ic);
        }

        foreach (var o in el.Elements("output".N()))
        {
            var oc = new OutputClause { Name = o.A("name"), TypeRef = o.A("typeRef") };
            var ov = o.Element("outputValues".N())?.Element("text".N());
            if (ov is not null) oc.OutputValues = new UnaryTests { Text = ov.Value };
            var def = o.Element("defaultOutputEntry".N())?.Elements().FirstOrDefault();
            if (def is not null) oc.DefaultOutputEntry = ReadExpression(def);
            dt.Outputs.Add(oc);
        }

        foreach (var r in el.Elements("rule".N()))
        {
            var dr = new DecisionRule();
            foreach (var i in r.Elements("inputEntry".N())) dr.InputEntry.Add(new UnaryTests { Text = i.Element("text".N())?.Value });
            foreach (var oe in r.Elements("outputEntry".N())) dr.OutputEntry.Add(new LiteralExpression { Text = oe.Element("text".N())?.Value });
            foreach (var an in r.Elements("annotationEntry".N())) dr.AnnotationEntry.Add(new RuleAnnotation { Text = an.Element("text".N())?.Value });
            dt.Rules.Add(dr);
        }
        return dt;
    }

    static DMNDI ReadDi(XElement di, Dictionary<string, DMNElement> idMap)
    {
        var root = new DMNDI();
        foreach (var d in di.Elements("DMNDiagram".D()))
        {
            var dia = new DMNDiagram { Name = d.A("name") };
            foreach (var e in d.Elements())
            {
                if (e.Name == "DMNShape".D())
                {
                    var s = new DMNShape();
                    var cref = e.A("dmnElementRef");
                    if (cref is not null && idMap.TryGetValue(cref.TrimStart('#'), out var be)) s.DmnElementRef = be;
                    var b = e.Element("Bounds".Dc());
                    if (b is not null) s.Bounds = new Bounds { X = b.Ad("x") ?? 0, Y = b.Ad("y") ?? 0, Width = b.Ad("width") ?? 0, Height = b.Ad("height") ?? 0 };
                    dia.Elements.Add(s);
                }
                else if (e.Name == "DMNEdge".D())
                {
                    var ed = new DMNEdge();
                    var cref = e.A("dmnElementRef");
                    if (cref is not null && idMap.TryGetValue(cref.TrimStart('#'), out var be)) ed.DmnElementRef = be;
                    foreach (var wp in e.Elements("waypoint".Di())) ed.Waypoints.Add(new Point { X = wp.Ad("x") ?? 0, Y = wp.Ad("y") ?? 0 });
                    dia.Elements.Add(ed);
                }
            }
            root.Diagrams.Add(dia);
        }
        return root;
    }
}