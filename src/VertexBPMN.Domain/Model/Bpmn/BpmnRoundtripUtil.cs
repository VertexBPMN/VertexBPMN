// for ObsoleteAttribute

using System.Xml.Linq;
using VertexBPMN.Domain.Model.Runtime;

namespace VertexBPMN.Domain.Model.Bpmn;

public static class BpmnRoundtripUtil
{
	public static BpmnModel MarkDirty(BpmnModel model)
	{
		if (model.RawMetadata == null)
		{
			return model;
		}

		if (model.RawMetadata.RoundtripDirty)
		{
			return model; // already marked
		}

		var rm = model.RawMetadata with { RoundtripDirty = true };
		return model with { RawMetadata = rm };
	}

	public static BpmnModel MarkDirtyOnAnyChange(BpmnModel model, string elementId)
	{
		var diagnostics = (model.Diagnostics ?? []).ToList();
		diagnostics.Add($"RT-Dirty:element:{elementId}");
		model = model with { Diagnostics = diagnostics };
		return MarkDirty(model);
	}

	public static BpmnModel ApplyAttributeChange(BpmnModel model, string elementId, string key, string value)
	{
		// Update task attributes if target is a task
		if ((model.Tasks ?? []).FirstOrDefault(t => t.Id == elementId) is { } task)
		{
			var attrs = task.Attributes == null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(task.Attributes, StringComparer.Ordinal);
			attrs[key] = value;
			task = key == "name" ? (task with { Attributes = attrs, Name = value }) : (task with { Attributes = attrs });

			var tasks = (model.Tasks ?? []).ToList();
			var idx = tasks.FindIndex(t => t.Id == elementId);
			tasks[idx] = task;
			model = model with { Tasks = tasks };
		}
		// (Could add similar handling for events, gateways etc.)
		return MarkDirtyOnAnyChange(model, elementId);
	}

	public static BpmnModel ApplyAttributeChangePartial(BpmnModel model, string elementId, string key, string value)
	{
		if (model.RawMetadata == null)
		{
			return model; // nothing to do
		}
		// mutate element (tasks only for now) without setting RoundtripDirty, track element id in PartiallyDirtyElements
		if ((model.Tasks ?? []).FirstOrDefault(t => t.Id == elementId) is { } task)
		{
			var attrs = task.Attributes == null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(task.Attributes, StringComparer.Ordinal);
			attrs[key] = value;
			task = key == "name" ? task with { Attributes = attrs, Name = value } : task with { Attributes = attrs };
			var tasks = (model.Tasks ?? []).ToList();
			var idx = tasks.FindIndex(t => t.Id == elementId);
			tasks[idx] = task;
			var diagnostics = (model.Diagnostics ?? []).ToList();
			diagnostics.Add($"RT-DirtyPartial:element:{elementId}");
			var dirtySet = model.RawMetadata.PartiallyDirtyElements != null ? new HashSet<string>(model.RawMetadata.PartiallyDirtyElements, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
			dirtySet.Add(elementId);
			var rm = model.RawMetadata with { PartiallyDirtyElements = dirtySet }; // keep RoundtripDirty false
			model = model with { Tasks = tasks, Diagnostics = diagnostics, RawMetadata = rm };
		}
		return model;
	}
}
