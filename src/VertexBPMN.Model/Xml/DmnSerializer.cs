using System.Xml.Linq;
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Domain.Model;

public static class DmnSerializer
{
    public static XDocument Write(DmnDefinitions defs)
    {
        var root = new XElement("definitions".N(),
            new XAttribute("id", defs.Id ?? "defs_1"),
            new XAttribute("name", defs.Name),
            new XAttribute("namespace", defs.Namespace),
            defs.ExpressionLanguage is null ? null : new XAttribute("expressionLanguage", defs.ExpressionLanguage),
            defs.TypeLanguage is null ? null : new XAttribute("typeLanguage", defs.TypeLanguage),
            new XAttribute(XNamespace.Xmlns + "dmn", Ns.DMN.NamespaceName.TrimEnd('/')),
            new XAttribute(XNamespace.Xmlns + "dmndi", Ns.DMNDI.NamespaceName.TrimEnd('/')),
            new XAttribute(XNamespace.Xmlns + "di", Ns.DI),
            new XAttribute(XNamespace.Xmlns + "dc", Ns.DC));

        foreach (var imp in defs.Imports)
            root.Add(new XElement("import".N(),
                new XAttribute("name", imp.Name),
                new XAttribute("importType", imp.ImportType),
                imp.LocationUri is null ? null : new XAttribute("locationURI", imp.LocationUri),
                new XAttribute("namespace", imp.Namespace)));

        foreach (var it in defs.ItemDefinitions) root.Add(WriteItemDefinition(it));
        foreach (var drg in defs.DrgElements) root.Add(WriteDRG(drg));
        if (defs.Dmndi is not null) root.Add(WriteDi(defs.Dmndi));

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
    }

    static XElement WriteItemDefinition(DmnItemDefinition i)
    {
        var x = new XElement("itemDefinition".N(),
            new XAttribute("id", i.Id ?? Guid.NewGuid().ToString("N")),
            new XAttribute("name", i.Name),
            new XAttribute("typeRef", i.TypeRef),
            new XAttribute("isCollection", i.IsCollection));
        if (i.TypeConstraint?.Text is not null) x.Add(new XElement("typeConstraint".N(), new XElement("text".N(), i.TypeConstraint.Text)));
        foreach (var c in i.ItemComponents) x.Add(WriteItemDefinition(c));
        return x;
    }

    static XElement WriteDRG(DmnDRGElement e) => e switch
    {
        DmnDecision d => WriteDecision(d),
        DmnInputData i => new XElement("inputData".N(), new XAttribute("id", i.Id ?? Guid.NewGuid().ToString("N")), new XAttribute("name", i.Name), WriteInformationItem("variable", i.Variable)),
        DmnBusinessKnowledgeModel bkm => new XElement("businessKnowledgeModel".N(), new XAttribute("id", bkm.Id ?? Guid.NewGuid().ToString("N")), new XAttribute("name", bkm.Name), bkm.EncapsulatedLogic is null ? null : WriteFunctionDefinition("encapsulatedLogic", bkm.EncapsulatedLogic)),
        DmnDecisionService ds => WriteDecisionService(ds),
        DmnKnowledgeSource ks => new XElement("knowledgeSource".N(), new XAttribute("id", ks.Id ?? Guid.NewGuid().ToString("N")), new XAttribute("name", ks.Name)),
        _ => new XElement("drgElement".N(), new XAttribute("id", e.Id ?? Guid.NewGuid().ToString("N")))
    };

    static XElement WriteDecision(DmnDecision d)
    {
        var x = new XElement("decision".N(), new XAttribute("id", d.Id ?? Guid.NewGuid().ToString("N")), new XAttribute("name", d.Name), WriteInformationItem("variable", d.Variable));
        foreach (var ir in d.InformationRequirements)
        {
            var xir = new XElement("informationRequirement".N());
            if (ir.RequiredDecision is not null) xir.Add(new XElement("requiredDecision".N(), new XAttribute("href", "#" + ir.RequiredDecision.Href)));
            if (ir.RequiredInput is not null) xir.Add(new XElement("requiredInput".N(), new XAttribute("href", "#" + ir.RequiredInput.Href)));
            x.Add(xir);
        }
        foreach (var kr in d.KnowledgeRequirements)
        {
            var xkr = new XElement("knowledgeRequirement".N());
            if (kr.RequiredKnowledge is not null) xkr.Add(new XElement("requiredKnowledge".N(), new XAttribute("href", "#" + kr.RequiredKnowledge.Href)));
            x.Add(xkr);
        }
        foreach (var ar in d.AuthorityRequirements)
        {
            var xar = new XElement("authorityRequirement".N());
            if (ar.RequiredAuthority is not null) xar.Add(new XElement("requiredAuthority".N(), new XAttribute("href", "#" + ar.RequiredAuthority.Href)));
            if (ar.RequiredDecision is not null) xar.Add(new XElement("requiredDecision".N(), new XAttribute("href", "#" + ar.RequiredDecision.Href)));
            if (ar.RequiredInput is not null) xar.Add(new XElement("requiredInput".N(), new XAttribute("href", "#" + ar.RequiredInput.Href)));
            x.Add(xar);
        }
        if (d.Expression is not null) x.Add(WriteExpression("decisionLogic", d.Expression));
        return x;
    }

    static XElement WriteDecisionService(DmnDecisionService ds)
    {
        var x = new XElement("decisionService".N(), new XAttribute("id", ds.Id ?? Guid.NewGuid().ToString("N")), new XAttribute("name", ds.Name), WriteInformationItem("variable", ds.Variable));
        foreach (var od in ds.OutputDecisions) x.Add(new XElement("outputDecision".N(), new XAttribute("href", "#" + od.Href)));
        foreach (var ed in ds.EncapsulatedDecisions) x.Add(new XElement("encapsulatedDecision".N(), new XAttribute("href", "#" + ed.Href)));
        foreach (var id in ds.InputDecisions) x.Add(new XElement("inputDecision".N(), new XAttribute("href", "#" + id.Href)));
        foreach (var ii in ds.InputDatas) x.Add(new XElement("inputData".N(), new XAttribute("href", "#" + ii.Href)));
        return x;
    }

    static XElement WriteInformationItem(string local, DmnInformationItem ii)
        => new XElement(local.N(), new XAttribute("id", ii.Id ?? Guid.NewGuid().ToString("N")), new XAttribute("name", ii.Name), new XAttribute("typeRef", ii.TypeRef ?? "Any"));

    static XElement WriteExpression(string local, DmnExpression e) => e switch
    {
        DmnLiteralExpression le => new XElement(local.N(), new XElement("literalExpression".N(), le.ExpressionLanguage is null ? null : new XAttribute("expressionLanguage", le.ExpressionLanguage), le.Text is null ? null : new XElement("text".N(), le.Text))),
        DmnDecisionTable dt => new XElement(local.N(), WriteDecisionTable(dt)),
        DmnInvocation inv => new XElement(local.N(), WriteInvocation(inv)),
        DmnFunctionDefinition fd => new XElement(local.N(), WriteFunctionDefinition("functionDefinition", fd)),
        _ => new XElement(local.N(), new XElement("literalExpression".N()))
    };

    static XElement WriteInvocation(DmnInvocation inv)
    {
        var x = new XElement("invocation".N(), WriteExpression("expression", inv.Expression));
        foreach (var b in inv.Bindings)
        {
            var xb = new XElement("binding".N(), new XElement("parameter".N(), new XAttribute("name", b.Parameter.Name)));
            if (b.Expression is not null) xb.Add(WriteExpression("expression", b.Expression));
            x.Add(xb);
        }
        return x;
    }

    static XElement WriteFunctionDefinition(string local, DmnFunctionDefinition fd)
    {
        var x = new XElement(local.N(), new XAttribute("kind", fd.Kind));
        foreach (var p in fd.FormalParameters) x.Add(new XElement("formalParameter".N(), new XAttribute("name", p.Name), new XAttribute("typeRef", p.TypeRef)));
        if (fd.Expression is not null) x.Add(WriteExpression("expression", fd.Expression));
        return x;
    }

    static XElement WriteDecisionTable(DmnDecisionTable dt)
    {
        var x = new XElement("decisionTable".N(), new XAttribute("hitPolicy", dt.HitPolicy.ToString()));
        if (dt.Aggregation is not null) x.Add(new XAttribute("aggregation", dt.Aggregation.ToString()));
        x.Add(new XAttribute("preferredOrientation", dt.PreferredOrientation.ToString()));
        if (dt.OutputLabel is not null) x.Add(new XAttribute("outputLabel", dt.OutputLabel));

        foreach (var i in dt.Inputs)
        {
            var xi = new XElement("input".N());
            if (i.InputExpression is not null) xi.Add(WriteExpression("inputExpression", i.InputExpression));
            if (i.InputValues?.Text is not null) xi.Add(new XElement("inputValues".N(), new XElement("text".N(), i.InputValues.Text)));
            x.Add(xi);
        }
        foreach (var o in dt.Outputs)
        {
            var xo = new XElement("output".N(), o.Name is null ? null : new XAttribute("name", o.Name), o.TypeRef is null ? null : new XAttribute("typeRef", o.TypeRef));
            if (o.OutputValues?.Text is not null) xo.Add(new XElement("outputValues".N(), new XElement("text".N(), o.OutputValues.Text)));
            if (o.DefaultOutputEntry is not null) xo.Add(WriteExpression("defaultOutputEntry", o.DefaultOutputEntry));
            x.Add(xo);
        }
        foreach (var r in dt.Rules)
        {
            var xr = new XElement("rule".N());
            foreach (var ie in r.InputEntries) xr.Add(new XElement("inputEntry".N(), ie.Text is null ? null : new XElement("text".N(), ie.Text)));
            foreach (var oe in r.OutputEntries) xr.Add(new XElement("outputEntry".N(), oe.Text is null ? null : new XElement("text".N(), oe.Text)));
            foreach (var an in r.AnnotationEntries) xr.Add(new XElement("annotationEntry".N(), an.Text is null ? null : new XElement("text".N(), an.Text)));
            x.Add(xr);
        }
        return x;
    }

    static XElement WriteDi(Dmndi diRoot)
    {
        var xdi = new XElement("DMNDI".D());
        foreach (var d in diRoot.DmnDiagrams)
        {
            var xd = new XElement("DMNDiagram".D(), d.Name is null ? null : new XAttribute("name", d.Name));
            foreach (var e in d.DmnDiagramElements)
            {
                if (e is DmnShape s)
                {
                    var xs = new XElement("DMNShape".D(), s.DmnElementRef?.Name is null ? null : new XAttribute("dmnElementRef", s.DmnElementRef.Name));
                    xs.Add(new XElement("Bounds".DC(), new XAttribute("x", s.Bounds.X), new XAttribute("y", s.Bounds.Y), new XAttribute("width", s.Bounds.Width), new XAttribute("height", s.Bounds.Height)));
                    xd.Add(xs);
                }
                else if (e is DmnEdge ed)
                {
                    var xe = new XElement("DMNEdge".D(), ed.DmnElementRef?.Name is null ? null : new XAttribute("dmnElementRef", ed.DmnElementRef.Name));
                    foreach (var p in ed.Waypoints) xe.Add(new XElement("waypoint".DI(), new XAttribute("x", p.X), new XAttribute("y", p.Y)));
                    xd.Add(xe);
                }
            }
            xdi.Add(xd);
        }
        return xdi;
    }
}