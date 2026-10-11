using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Serialization;

public partial class BpmnSerializer
{
	private static void RestoreNamespacePrefixOrder(bool strict, BpmnRawMetadata? raw, XElement definitions)
	{
		if (strict && raw?.NamespacePrefixes is { Count: > 0 })
		{
			List<NamespacePrefix> originalOrder = [.. raw.NamespacePrefixes];
			foreach (var np in originalOrder)
			{
				if (string.IsNullOrEmpty(np.Prefix))
				{
					definitions.SetAttributeValue("xmlns", np.Uri);
				}
				else
				{
					definitions.SetAttributeValue(XNamespace.Xmlns + np.Prefix, np.Uri);
				}
			}

			var hasBpmnPrefix = originalOrder.Any(p => p.Uri == Bpmn.NamespaceName && p.Prefix == "bpmn");
			var hasDefaultBpmn = originalOrder.Any(p => p.Uri == Bpmn.NamespaceName && string.IsNullOrEmpty(p.Prefix));
			if (!hasBpmnPrefix && !hasDefaultBpmn)
			{
				definitions.SetAttributeValue(XNamespace.Xmlns + "bpmn", Bpmn.NamespaceName);
			}
		}
		else
		{
			definitions.SetAttributeValue(XNamespace.Xmlns + "bpmn", Bpmn.NamespaceName);
		}
	}

	private static void RestoreExtensionNamespaceDeclarations(bool strict, BpmnRawMetadata? raw, XElement definitions)
	{
		if (strict && raw?.RawExtensionElements != null)
		{
			var known = new HashSet<string>(definitions.Attributes().Where(a => a.IsNamespaceDeclaration).Select(a => a.Value), StringComparer.Ordinal);
			foreach (var kv in raw.RawExtensionElements)
			{
				foreach (var attr in kv.Value.Attributes())
				{
					if (!attr.IsNamespaceDeclaration)
					{
						continue;
					}

					var uri = attr.Value;
					if (known.Add(uri))
					{
						var prefix = attr.Name.LocalName == "xmlns" ? string.Empty : attr.Name.LocalName;
						if (string.IsNullOrEmpty(prefix))
						{
							prefix = "ns_ext" + known.Count;
						}

						definitions.SetAttributeValue(XNamespace.Xmlns + prefix, uri);
					}
				}
			}
		}
	}

	private static void RestoreDefinitionsAttributes(bool strict, BpmnRawMetadata? raw, HashSet<string> vendorNs, XElement definitions)
	{
		if (strict && raw?.DefinitionsAttributes != null)
		{
			foreach (var kv in raw.DefinitionsAttributes)
			{
				var local = ParseLocalName(kv.Key);
				var nsUri = ParseNamespace(kv.Key);
				if (nsUri == null)
				{
					if (definitions.Attribute(local) == null)
					{
						definitions.Add(new XAttribute(local, kv.Value));
					}
				}
				else
				{
					var xname = XName.Get(local, nsUri);
					if (definitions.Attribute(xname) == null)
					{
						definitions.Add(new XAttribute(xname, kv.Value));
					}
				}
			}
		}
		else if (!strict)
		{
			foreach (var uri in vendorNs)
			{
				if (!WellKnownPrefixes.TryGetValue(uri, out var prefix))
				{
					prefix = uri.Contains('/', StringComparison.Ordinal) ? uri.TrimEnd('/').Split('/').Last().Replace('.', '_').Replace('-', '_') : "ns";
				}

				definitions.SetAttributeValue(XNamespace.Xmlns + prefix, uri);
			}
		}
	}

	private static void RestoreProcessAttributes(bool strict, BpmnRawMetadata? raw, XElement proc)
	{
		if (strict && raw?.ProcessAttributes != null)
		{
			foreach (var kv in raw.ProcessAttributes)
			{
				var local = ParseLocalName(kv.Key);
				if (local == "id")
				{
					continue;
				}

				var nsUri = ParseNamespace(kv.Key);
				if (nsUri == null)
				{
					if (proc.Attribute(local) == null)
					{
						proc.Add(new XAttribute(local, kv.Value));
					}
				}
				else
				{
					var xname = XName.Get(local, nsUri);
					if (proc.Attribute(xname) == null)
					{
						proc.Add(new XAttribute(xname, kv.Value));
					}
				}
			}

			if (raw.RawDocumentation != null && raw.RawDocumentation.TryGetValue("__process", out var pdocs))
			{
				foreach (var d in pdocs)
				{
					proc.Add(new XElement(d));
				}
			}
		}
	}

	private static void NestNormalizedSubprocessNodes(BpmnModel model, bool strict, XElement proc)
	{
		if (!strict)
		{
			var nodes = proc.Elements().Where(element => element.Attribute("id") is not null).ToDictionary(element => element.Attribute("id")!.Value, StringComparer.Ordinal);
			var owners = (model.Subprocesses ?? []).Select(item => (item.Id, item.SubprocessId)).Concat((model.Tasks ?? []).Select(item => (item.Id, item.SubprocessId))).Concat((model.Events ?? []).Select(item => (item.Id, item.SubprocessId))).Concat((model.Gateways ?? []).Select(item => (item.Id, item.SubprocessId))).Concat((model.SequenceFlows ?? []).Select(item => (item.Id, item.SubprocessId)));
			foreach (var (id, ownerId) in owners)
			{
				if (ownerId is not null && nodes.TryGetValue(id, out var child) && nodes.TryGetValue(ownerId, out var owner))
				{
					child.Remove();
					owner.Add(child);
				}
			}
		}
	}

	private static void RestoreLaneSets(bool strict, BpmnRawMetadata? raw, XElement proc)
	{
		if (strict && raw?.RawLanes is { Count: > 0 })
		{
			var laneSets = raw.RawLanes.Where(x => x.Name.LocalName == "laneSet").ToList();
			if (laneSets.Count > 0)
			{
				foreach (var ls in laneSets)
				{
					proc.Add(new XElement(ls));
				}

				var laneIdsInSets = new HashSet<string>(laneSets.SelectMany(ls => ls.Elements().Where(e => e.Name.LocalName == "lane").Select(e => (string?)e.Attribute("id")).Where(id => !string.IsNullOrEmpty(id))!).OfType<string>(), StringComparer.Ordinal);
				foreach (var ln in raw.RawLanes.Where(x => x.Name.LocalName == "lane"))
				{
					var id = (string?)ln.Attribute("id");
					if (!string.IsNullOrEmpty(id) && laneIdsInSets.Contains(id))
					{
						continue;
					}

					proc.Add(new XElement(ln));
				}
			}
			else
			{
				foreach (var ln in raw.RawLanes)
				{
					proc.Add(new XElement(ln));
				}
			}
		}
	}

	private void FillMissingStrictFlowReferences(bool strict, Dictionary<string, List<string>>? fallbackIncoming, Dictionary<string, List<string>>? fallbackOutgoing, XElement proc)
	{
		if (strict && PreserveGeneratedIfMissing && fallbackIncoming != null && fallbackOutgoing != null)
		{
			var nodeById = proc.Elements().Where(e => e.Attribute("id") != null).ToDictionary(e => (string)e.Attribute("id")!, e => e, StringComparer.Ordinal);
			foreach (var kv in fallbackIncoming)
			{
				if (!nodeById.TryGetValue(kv.Key, out var el))
				{
					continue;
				}

				if (!el.Elements(Bpmn + "incoming").Any())
				{
					foreach (var fid in kv.Value)
					{
						el.Add(new XElement(Bpmn + "incoming", fid));
					}
				}
			}

			foreach (var kv in fallbackOutgoing)
			{
				if (!nodeById.TryGetValue(kv.Key, out var el))
				{
					continue;
				}

				if (!el.Elements(Bpmn + "outgoing").Any())
				{
					foreach (var fid in kv.Value)
					{
						el.Add(new XElement(Bpmn + "outgoing", fid));
					}
				}
			}
		}
	}

	private static void AppendNormalizedDataObjects(BpmnModel model, bool strict, XElement proc)
	{
		if (!strict)
		{
			foreach (var d in model.DataObjects ?? [])
			{
				proc.Add(new XElement(Bpmn + "dataObject", new XAttribute("id", d.Id), d.Name != null ? new XAttribute("name", d.Name) : null));
			}

			foreach (var dr in model.DataObjectReferences ?? [])
			{
				proc.Add(new XElement(Bpmn + "dataObjectReference", new XAttribute("id", dr.Id), new XAttribute("dataObjectRef", dr.DataObjectRef)));
			}

			foreach (var ds in model.DataStores ?? [])
			{
				proc.Add(new XElement(Bpmn + "dataStore", new XAttribute("id", ds.Id), ds.Name != null ? new XAttribute("name", ds.Name) : null));
			}

			foreach (var dsr in model.DataStoreReferences ?? [])
			{
				proc.Add(new XElement(Bpmn + "dataStoreReference", new XAttribute("id", dsr.Id), new XAttribute("dataStoreRef", dsr.DataStoreRef)));
			}

			foreach (var p in model.Properties ?? [])
			{
				proc.Add(new XElement(Bpmn + "property", new XAttribute("id", p.Id), p.Name != null ? new XAttribute("name", p.Name) : null));
			}
		}
	}

	private static void AppendNormalizedActivityIo(BpmnModel model, bool strict, XElement proc)
	{
		if (!strict)
		{
			foreach (var io in model.ActivityIo ?? [])
			{
				var act = proc.Elements().FirstOrDefault(e => (string?)e.Attribute("id") == io.ActivityId);
				if (act == null)
				{
					continue;
				}

				var ioSpec = new XElement(Bpmn + "ioSpecification");
				foreach (var i in io.DataInputs)
				{
					ioSpec.Add(new XElement(Bpmn + "dataInput", new XAttribute("id", i.Id), i.Name != null ? new XAttribute("name", i.Name) : null));
				}

				foreach (var o in io.DataOutputs)
				{
					ioSpec.Add(new XElement(Bpmn + "dataOutput", new XAttribute("id", o.Id), o.Name != null ? new XAttribute("name", o.Name) : null));
				}

				act.Add(ioSpec);
				foreach (var ina in io.InputAssociations)
				{
					act.Add(new XElement(Bpmn + "dataInputAssociation", new XElement(Bpmn + "sourceRef", ina.SourceRef), new XElement(Bpmn + "targetRef", ina.TargetRef)));
				}

				foreach (var oa in io.OutputAssociations)
				{
					act.Add(new XElement(Bpmn + "dataOutputAssociation", new XElement(Bpmn + "sourceRef", oa.SourceRef), new XElement(Bpmn + "targetRef", oa.TargetRef)));
				}
			}
		}
	}

	private static void AppendNormalizedDiagram(BpmnModel model, bool strict, BpmnRawMetadata? raw, XElement definitions)
	{
		if (!strict && ((model.Shapes?.Count > 0) || (model.Edges?.Count > 0)))
		{
			var bpmndi = (XNamespace)"http://www.omg.org/spec/BPMN/20100524/DI";
			var omgdc = (XNamespace)"http://www.omg.org/spec/DD/20100524/DC";
			var omgdi = (XNamespace)"http://www.omg.org/spec/DD/20100524/DI";
			if (definitions.Attribute(XNamespace.Xmlns + "bpmndi") == null)
			{
				definitions.SetAttributeValue(XNamespace.Xmlns + "bpmndi", bpmndi.NamespaceName);
			}

			if (definitions.Attribute(XNamespace.Xmlns + "omgdc") == null)
			{
				definitions.SetAttributeValue(XNamespace.Xmlns + "omgdc", omgdc.NamespaceName);
			}

			if (definitions.Attribute(XNamespace.Xmlns + "omgdi") == null)
			{
				definitions.SetAttributeValue(XNamespace.Xmlns + "omgdi", omgdi.NamespaceName);
			}

			var diagram = new XElement(bpmndi + "BPMNDiagram", new XAttribute("id", $"{model.ProcessId}_diagram"));
			var plane = new XElement(bpmndi + "BPMNPlane", new XAttribute("bpmnElement", model.ProcessId));
			diagram.Add(plane);
			if (model.Shapes != null)
			{
				foreach (var s in model.Shapes)
				{
					plane.Add(new XElement(bpmndi + "BPMNShape", new XAttribute("id", s.Id), new XAttribute("bpmnElement", s.BpmnElementId), new XElement(omgdc + "Bounds", new XAttribute("x", s.X), new XAttribute("y", s.Y), new XAttribute("width", s.Width), new XAttribute("height", s.Height))));
				}
			}

			if (model.Edges != null)
			{
				foreach (var e in model.Edges)
				{
					plane.Add(new XElement(bpmndi + "BPMNEdge", new XAttribute("id", e.Id), new XAttribute("bpmnElement", e.BpmnElementId), e.Waypoints.Select(wp => new XElement(omgdi + "waypoint", new XAttribute("x", wp.X), new XAttribute("y", wp.Y)))));
				}
			}

			definitions.Add(diagram);
		}
		else if (strict && raw?.RawDiRoot != null)
		{
			var bpmndi = (XNamespace)"http://www.omg.org/spec/BPMN/20100524/DI";
			if (definitions.Attribute(XNamespace.Xmlns + "bpmndi") == null)
			{
				definitions.SetAttributeValue(XNamespace.Xmlns + "bpmndi", bpmndi.NamespaceName);
			}

			foreach (var diag in raw.RawDiRoot.Elements(bpmndi + "BPMNDiagram"))
			{
				definitions.Add(new XElement(diag));
			}
		}
	}

	private void AppendMissingCollaboration(BpmnModel model, bool strictRequested, bool strict, XElement definitions)
	{
		if ((strict || (strictRequested && PreserveGeneratedIfMissing)) && PreserveGeneratedIfMissing)
		{
			var procEl = definitions.Elements(Bpmn + "process").FirstOrDefault(e => (string?)e.Attribute("id") == model.ProcessId);
			if (procEl != null)
			{
				var nodeById = procEl.Elements().Where(e => e.Attribute("id") != null).ToDictionary(e => (string)e.Attribute("id")!, e => e, StringComparer.Ordinal);
				foreach (var flow in model.SequenceFlows ?? [])
				{
					if (nodeById.TryGetValue(flow.SourceRef, out var src))
					{
						var hasOut = src.Elements(Bpmn + "outgoing").Any(x => (string)x == flow.Id);
						if (!hasOut)
						{
							src.Add(new XElement(Bpmn + "outgoing", flow.Id));
						}
					}

					if (nodeById.TryGetValue(flow.TargetRef, out var tgt))
					{
						var hasIn = tgt.Elements(Bpmn + "incoming").Any(x => (string)x == flow.Id);
						if (!hasIn)
						{
							tgt.Add(new XElement(Bpmn + "incoming", flow.Id));
						}
					}
				}
			}
		}
	}

}

