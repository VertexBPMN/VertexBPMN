using System.Linq;
using VertexBPMN.Domain.Model.Bpmn.Activities;
using VertexBPMN.Domain.Model.Bpmn.Collaboration;
using VertexBPMN.Domain.Model.Bpmn.Common.Flow;
using VertexBPMN.Domain.Model.Bpmn.Common.Items;
using VertexBPMN.Domain.Model.Bpmn.Common.Messages;
using VertexBPMN.Domain.Model.Bpmn.Events;
using VertexBPMN.Domain.Model.Bpmn.Foundation;
using VertexBPMN.Domain.Model.Bpmn.Gateways;
using VertexBPMN.Domain.Model.Bpmn.Infrastructure;
using VertexBPMN.Domain.Model.Bpmn.Processes;
using VertexBPMN.Domain.Model.Bpmn.Xml;
using Xunit;

namespace VertexBPMN.Domain.Model.Bpmn.Tests;

public class RoundtripFullTests
{
    [Fact]
    public void Model_Constructs()
    {
        var item = new ItemDefinition { Id = "itemDef_Order", StructureRef = "xsd:string" };
        var msg = new Message { Id = "msg_Order", Name = "Order", ItemRef = item };

        var p = new Process { Id = "proc", Name = "Main", IsExecutable = true };
        var se = new StartEvent { Id = "s" };
        var t = new Task { Id = "t", Name = "Do" };
        var gw = new ExclusiveGateway { Id = "g" };
        var ee = new EndEvent { Id = "e" };

        p.FlowElements.Add(se); p.FlowElements.Add(t); p.FlowElements.Add(gw); p.FlowElements.Add(ee);
        p.FlowElements.Add(new SequenceFlow { Id = "f1", SourceRef = se, TargetRef = t });
        p.FlowElements.Add(new SequenceFlow { Id = "f2", SourceRef = t, TargetRef = gw });
        p.FlowElements.Add(new SequenceFlow { Id = "f3", SourceRef = gw, TargetRef = ee });

        var collab = new Collaboration.Collaboration { Id = "c" };
        collab.Participants.Add(new Participant { Id = "part1", Name = "Org", ProcessRef = p });

        var defs = new Definitions(id: "defs", targetNamespace: "http://example.com/bpmn");
        defs.RootElements.Add(item); defs.RootElements.Add(msg); defs.RootElements.Add(p); defs.RootElements.Add(collab);

        Assert.Equal("http://example.com/bpmn", defs.TargetNamespace);
    }
    [Fact]
    public void Basic_Write_Works()
    {
        var proc = new Process { Id = "proc1" };
        var start = new StartEvent { Id = "s" };
        var task = new Task { Id = "t" };
        var gw = new ExclusiveGateway { Id = "g" };
        var end = new EndEvent { Id = "e" };
        var f1 = new SequenceFlow { Id = "f1", SourceRef = start, TargetRef = task };
        var f2 = new SequenceFlow { Id = "f2", SourceRef = task, TargetRef = gw };
        var f3 = new SequenceFlow { Id = "f3", SourceRef = gw, TargetRef = end };
        proc.FlowElements.Add(start); proc.FlowElements.Add(task); proc.FlowElements.Add(gw); proc.FlowElements.Add(end);
        proc.FlowElements.Add(f1); proc.FlowElements.Add(f2); proc.FlowElements.Add(f3);
        var defs = new Definitions(id: "defs", targetNamespace: "http://example.com/bpmn");
        defs.RootElements.Add(proc);
        defs.RootElements.Add(new Collaboration.Collaboration { Id = "c1" });
        var doc = BpmnWriter.Write(defs);
        Assert.NotNull(doc);
    }

    [Fact]
    public void Full_Roundtrip_Works()
    {
        var item = new ItemDefinition { Id = "itemDef_Order", StructureRef = "xsd:string" };
        var msg = new Message { Id = "msg_Order", Name = "Order", ItemRef = item };

        var p = new Process { Id = "proc", Name = "Main", IsExecutable = true };
        var se = new StartEvent { Id = "s" };
        var t = new Task { Id = "t", Name = "Do" };
        var gw = new ExclusiveGateway { Id = "g" };
        var ee = new EndEvent { Id = "e" };

        p.FlowElements.Add(se);
        p.FlowElements.Add(t);
        p.FlowElements.Add(gw);
        p.FlowElements.Add(ee);
        p.FlowElements.Add(new SequenceFlow { Id = "f1", SourceRef = se, TargetRef = t });
        p.FlowElements.Add(new SequenceFlow { Id = "f2", SourceRef = t, TargetRef = gw });
        p.FlowElements.Add(new SequenceFlow { Id = "f3", SourceRef = gw, TargetRef = ee });

        var collab = new Collaboration.Collaboration { Id = "c" };
        collab.Participants.Add(new Participant { Id = "part1", Name = "Org", ProcessRef = p });

        var defs = new Definitions(id: "defs", targetNamespace: "http://example.com/bpmn");
        defs.RootElements.AddRange(new RootElement[] { item, msg, p, collab });

        var xml = BpmnWriter.Write(defs);
        var defs2 = BpmnReader.Read(xml);

        Assert.Equal(defs.TargetNamespace, defs2.TargetNamespace);
        Assert.True(defs2.RootElements.OfType<Process>().Any());
        Assert.True(defs2.RootElements.OfType<Collaboration.Collaboration>().Any());
    }
}