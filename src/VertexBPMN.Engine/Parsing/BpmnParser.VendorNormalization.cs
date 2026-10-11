using System.Xml.Linq;
using VertexBPMN.Domain.Model.Bpmn;

namespace VertexBPMN.Engine.Parsing;

public partial class BpmnParser
{
	private void NormalizeVendorExtensionChild(XElement child, Dictionary<string, string> bucket)
	{
		var local = child.Name.LocalName;
		switch (child.Name.NamespaceName)
		{
			case BpmnXmlNamespaces.Camunda:
				NormalizeCamundaExtension(child, local, bucket);
				break;
			case BpmnXmlNamespaces.Zeebe:
				NormalizeZeebeExtension(child, local, bucket);
				break;
			case BpmnXmlNamespaces.Flowable:
				NormalizeFlowableExtension(child, local, bucket);
				break;
			case BpmnXmlNamespaces.Activiti:
				NormalizeActivitiExtension(child, local, bucket);
				break;
			case BpmnXmlNamespaces.Cib:
				NormalizeCibExtension(child, local, bucket);
				break;
			case BpmnXmlNamespaces.Jbpm:
				NormalizeJbpmExtension(child, local, bucket);
				break;
			case BpmnXmlNamespaces.Osmanthus:
				NormalizeOsmanthusExtension(child, local, bucket);
				break;
			case BpmnXmlNamespaces.Alfresco:
				NormalizeAlfrescoExtension(child, local, bucket);
				break;
			case BpmnXmlNamespaces.Mcp:
				NormalizeMcpExtension(child, local, bucket);
				break;
			default:
				if (_options.NormalizeUnknownVendorExtensions && child.Name.NamespaceName.Length > 0)
				{
					NormalizeUnknownVendorExtension(child, local, bucket);
				}
				break;
		}
	}

	private static void NormalizeMcpExtension(XElement child, string local, Dictionary<string, string> bucket)
	{
		if (local == "mcpServiceTask")
		{
			foreach (var attr in child.Attributes())
			{
				if (attr.IsNamespaceDeclaration)
				{
					continue;
				}

				if (!string.IsNullOrEmpty(attr.Value))
				{
					bucket[$"mcp:mcpServiceTask.{attr.Name.LocalName}"] = attr.Value;
				}
			}
		}
	}

	private static void NormalizeUnknownVendorExtension(XElement child, string local, Dictionary<string, string> bucket)
	{
		// Versuche Präfix zu ermitteln (falls nicht vorhanden -> generic)
		var prefix = child.GetPrefixOfNamespace(child.Name.Namespace);
		if (string.IsNullOrEmpty(prefix))
		{
			// Fallback Kürzel generieren (ns)
			prefix = "ns";
		}
		// Sicherstellen dass nicht einer der bekannten Prefixe kollidiert ohne definierten Namespace
		// (zero-break – ignorieren wenn zufällig gleich)
		foreach (var attr in child.Attributes())
		{
			if (attr.IsNamespaceDeclaration)
			{
				continue;
			}

			if (attr.Name.LocalName is "id" or "type" or "name")
			{
				continue;
			}

			if (attr.Value.Length == 0)
			{
				continue;
			}

			var key = $"{prefix}:{local}.{attr.Name.LocalName}";
			if (!bucket.ContainsKey(key))
			{
				bucket[key] = attr.Value;
			}
		}
		// Textinhalt optional
		if (!child.HasElements && !child.HasAttributes && !string.IsNullOrWhiteSpace(child.Value))
		{
			var k = $"{prefix}:{local}.__text";
			if (!bucket.ContainsKey(k))
			{
				bucket[k] = child.Value.Trim();
			}
		}
	}

	private static string NextIndexedKey(Dictionary<string, string> bucket, string baseKey)
	{
		if (!bucket.ContainsKey(baseKey + "#1"))
		{
			return baseKey + "#1";
		}
		var i = 2;
		while (bucket.ContainsKey(baseKey + "#" + i))
		{
			i++;
		}

		return baseKey + "#" + i;
	}

	private static void NormalizeCamundaFormField(XElement child, Dictionary<string, string> bucket)
	{

		var fid = child.Attribute("id")?.Value;
		if (!string.IsNullOrEmpty(fid))
		{
			var fType = child.Attribute("type")?.Value;
			var fName = child.Attribute("name")?.Value;
			if (!string.IsNullOrEmpty(fType))
			{
				bucket[$"camunda:formField.{fid}.type"] = fType;
			}

			if (!string.IsNullOrEmpty(fName))
			{
				bucket[$"camunda:formField.{fid}.name"] = fName;
			}
		}


	}

	private static void NormalizeCamundaFormData(XElement child, Dictionary<string, string> bucket)
	{


		bucket["camunda:formData"] = child.Name.LocalName;

		child.Elements().ToList().ForEach(ff =>
		{
			var fid = ff.Attribute("id")?.Value;
			if (!string.IsNullOrEmpty(fid))
			{
				var fType = ff.Attribute("type")?.Value;
				var fName = ff.Attribute("name")?.Value;
				var flabel = ff.Attribute("label")?.Value;
				if (!string.IsNullOrEmpty(fType))
				{
					bucket[$"camunda:formField.{fid}.type"] = fType;
				}

				if (!string.IsNullOrEmpty(fName))
				{
					bucket[$"camunda:formField.{fid}.name"] = fName;
				}

				if (!string.IsNullOrEmpty(flabel))
				{
					bucket[$"camunda:formField.{fid}.label"] = flabel;
				}
			}
		});


	}

	private static void NormalizeCamundaTaskListener(XElement child, Dictionary<string, string> bucket)
	{
		const string baseKey = "camunda:taskListener";
		var idxKey = NextIndexedKey(bucket, baseKey);
		var ev = child.Attribute("event")?.Value;
		var clazz = child.Attribute("class")?.Value;
		var expr = child.Attribute("expression")?.Value;
		if (!string.IsNullOrEmpty(ev))
		{
			bucket[$"{idxKey}.event"] = ev;
		}
		if (!string.IsNullOrEmpty(clazz))
		{
			bucket[$"{idxKey}.class"] = clazz;
		}
		if (!string.IsNullOrEmpty(expr))
		{
			bucket[$"{idxKey}.expression"] = expr;
		}
	}

	private static void NormalizeCamundaExtension(XElement child, string local, Dictionary<string, string> bucket)
	{
		switch (local)
		{
			case "assignee":
				if (child.Attribute("value")?.Value is { } cav && cav.Length > 0)
				{
					bucket["camunda:assignee"] = cav;
				}

				break;
			case "formField":
				NormalizeCamundaFormField(child, bucket);
				break;
			case "formData":
				NormalizeCamundaFormData(child, bucket);
				break;
			case "properties":
				foreach (var prop in child.Elements(XName.Get("property", BpmnXmlNamespaces.Camunda)))
				{
					var pname = prop.Attribute("name")?.Value;
					var pval = prop.Attribute("value")?.Value;
					if (!string.IsNullOrEmpty(pname) && pval != null)
					{
						bucket[$"camunda:property.{pname}"] = pval;
					}
				}
				break;
			case "taskListener":
				NormalizeCamundaTaskListener(child, bucket);
				break;
		}
	}

	private static void NormalizeZeebeIoMapping(XElement child, Dictionary<string, string> bucket)
	{
		foreach (var input in child.Elements(XName.Get("input", BpmnXmlNamespaces.Zeebe)))
		{
			var src = input.Attribute("source")?.Value;
			var tgt = input.Attribute("target")?.Value;
			if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(tgt))
			{
				bucket[$"zeebe:ioMapping.input.{tgt}"] = src;
			}
		}
		foreach (var output in child.Elements(XName.Get("output", BpmnXmlNamespaces.Zeebe)))
		{
			var src = output.Attribute("source")?.Value;
			var tgt = output.Attribute("target")?.Value;
			if (!string.IsNullOrEmpty(src) && !string.IsNullOrEmpty(tgt))
			{
				bucket[$"zeebe:ioMapping.output.{tgt}"] = src;
			}
		}
	}

	private static void NormalizeZeebeExtension(XElement child, string local, Dictionary<string, string> bucket)
	{
		switch (local)
		{
			case "taskDefinition":
				if (child.Attribute("type")?.Value is { } t && t.Length > 0)
				{
					bucket["zeebe:taskDefinition.type"] = t;
				}

				break;
			case "ioMapping":
				NormalizeZeebeIoMapping(child, bucket);
				break;
			case "taskHeaders":
				foreach (var header in child.Elements(XName.Get("header", BpmnXmlNamespaces.Zeebe)))
				{
					var hKey = header.Attribute("key")?.Value;
					var hVal = header.Attribute("value")?.Value;
					if (!string.IsNullOrEmpty(hKey) && hVal != null)
					{
						bucket[$"zeebe:taskHeaders.{hKey}"] = hVal;
					}
				}
				break;
		}
	}

	private static void NormalizeFlowableFormField(XElement child, Dictionary<string, string> bucket)
	{
		var fid = child.Attribute("id")?.Value;
		if (!string.IsNullOrEmpty(fid))
		{
			var fType = child.Attribute("type")?.Value;
			var fName = child.Attribute("name")?.Value;
			if (!string.IsNullOrEmpty(fType))
			{
				bucket[$"flowable:formField.{fid}.type"] = fType;
			}

			if (!string.IsNullOrEmpty(fName))
			{
				bucket[$"flowable:formField.{fid}.name"] = fName;
			}
		}
	}

	private static void NormalizeFlowableTaskListener(XElement child, Dictionary<string, string> bucket)
	{
		const string baseKey = "flowable:taskListener";
		var idxKey = NextIndexedKey(bucket, baseKey);
		var ev = child.Attribute("event")?.Value;
		var clazz = child.Attribute("class")?.Value;
		var expr = child.Attribute("expression")?.Value;
		if (!string.IsNullOrEmpty(ev))
		{
			bucket[$"{idxKey}.event"] = ev;
		}
		if (!string.IsNullOrEmpty(clazz))
		{
			bucket[$"{idxKey}.class"] = clazz;
		}
		if (!string.IsNullOrEmpty(expr))
		{
			bucket[$"{idxKey}.expression"] = expr;
		}
	}

	private static void NormalizeFlowableExtension(XElement child, string local, Dictionary<string, string> bucket)
	{
		switch (local)
		{
			case "assignee":
				if (child.Attribute("value")?.Value is { } fav && fav.Length > 0)
				{
					bucket["flowable:assignee"] = fav;
				}

				break;
			case "formField":
				NormalizeFlowableFormField(child, bucket);
				break;

			case "taskListener":
				NormalizeFlowableTaskListener(child, bucket);
				break;
		}
	}

	private static void NormalizeActivitiFormProperty(XElement child, Dictionary<string, string> bucket)
	{
		var fid = child.Attribute("id")?.Value;
		if (!string.IsNullOrEmpty(fid))
		{
			var fType = child.Attribute("type")?.Value;
			var fName = child.Attribute("name")?.Value;
			if (!string.IsNullOrEmpty(fType))
			{
				bucket[$"activiti:formProperty.{fid}.type"] = fType;
			}

			if (!string.IsNullOrEmpty(fName))
			{
				bucket[$"activiti:formProperty.{fid}.name"] = fName;
			}

			foreach (var attr in child.Attributes())
			{
				if (attr.IsNamespaceDeclaration)
				{
					continue;
				}

				if (attr.Name.LocalName is "id" or "type" or "name")
				{
					continue;
				}

				if (attr.Value.Length == 0)
				{
					continue;
				}

				bucket[$"activiti:formProperty.{fid}.{attr.Name.LocalName}"] = attr.Value;
			}
		}
	}

	private static void NormalizeActivitiTaskListener(XElement child, Dictionary<string, string> bucket)
	{

		const string baseKey = "activiti:taskListener";
		var idxKey = NextIndexedKey(bucket, baseKey);
		var ev = child.Attribute("event")?.Value;
		var clazz = child.Attribute("class")?.Value;
		var expr = child.Attribute("expression")?.Value;
		var del = child.Attribute("delegateExpression")?.Value;
		if (!string.IsNullOrEmpty(ev))
		{
			bucket[$"{idxKey}.event"] = ev;
		}

		if (!string.IsNullOrEmpty(clazz))
		{
			bucket[$"{idxKey}.class"] = clazz;
		}

		if (!string.IsNullOrEmpty(expr))
		{
			bucket[$"{idxKey}.expression"] = expr;
		}

		if (!string.IsNullOrEmpty(del))
		{
			bucket[$"{idxKey}.delegateExpression"] = del;
		}



	}

	private static void NormalizeActivitiExecutionListener(XElement child, Dictionary<string, string> bucket)
	{

		const string baseKey = "activiti:executionListener";
		var idxKey = NextIndexedKey(bucket, baseKey);
		var ev = child.Attribute("event")?.Value;
		var clazz = child.Attribute("class")?.Value;
		var expr = child.Attribute("expression")?.Value;
		var del = child.Attribute("delegateExpression")?.Value;
		if (!string.IsNullOrEmpty(ev))
		{
			bucket[$"{idxKey}.event"] = ev;
		}

		if (!string.IsNullOrEmpty(clazz))
		{
			bucket[$"{idxKey}.class"] = clazz;
		}

		if (!string.IsNullOrEmpty(expr))
		{
			bucket[$"{idxKey}.expression"] = expr;
		}

		if (!string.IsNullOrEmpty(del))
		{
			bucket[$"{idxKey}.delegateExpression"] = del;
		}



	}

	private static void NormalizeActivitiExtension(XElement child, string local, Dictionary<string, string> bucket)
	{
		switch (local)
		{
			case "formProperty":
				NormalizeActivitiFormProperty(child, bucket);
				break;

			case "taskListener":
				NormalizeActivitiTaskListener(child, bucket);
				break;
			case "executionListener":
				NormalizeActivitiExecutionListener(child, bucket);
				break;
			case "candidateUsers":
				if (child.Attribute("value")?.Value is { } cu && cu.Length > 0)
				{
					bucket["activiti:candidateUsers"] = cu;
				}

				break;
			case "candidateGroups":
				if (child.Attribute("value")?.Value is { } cg && cg.Length > 0)
				{
					bucket["activiti:candidateGroups"] = cg;
				}

				break;
		}
	}

	private static void NormalizeCibFormField(XElement child, Dictionary<string, string> bucket)
	{
		var fid = child.Attribute("id")?.Value;
		if (!string.IsNullOrEmpty(fid))
		{
			var fType = child.Attribute("type")?.Value;
			var fName = child.Attribute("name")?.Value;
			if (!string.IsNullOrEmpty(fType))
			{
				bucket[$"cib:formField.{fid}.type"] = fType;
			}

			if (!string.IsNullOrEmpty(fName))
			{
				bucket[$"cib:formField.{fid}.name"] = fName;
			}
		}
	}

	private static void NormalizeCibConnector(XElement child, Dictionary<string, string> bucket)
	{
		var cid = child.Attribute("id")?.Value;
		if (string.IsNullOrEmpty(cid))
		{
			cid = NextIndexedKey(bucket, "cib:connector").Split('#')[1]; // fallback index part
		}
		var idKey = string.IsNullOrEmpty(child.Attribute("id")?.Value) ? NextIndexedKey(bucket, "cib:connector") : $"cib:connector.{cid}";
		var realKeyPrefix = idKey.StartsWith("cib:connector#", StringComparison.Ordinal) ? idKey : $"cib:connector.{cid}";
		var cType = child.Attribute("type")?.Value;
		var url = child.Attribute("url")?.Value;
		if (!string.IsNullOrEmpty(cType))
		{
			bucket[$"{realKeyPrefix}.type"] = cType;
		}
		if (!string.IsNullOrEmpty(url))
		{
			bucket[$"{realKeyPrefix}.url"] = url;
		}
	}

	private static void NormalizeCibExtension(XElement child, string local, Dictionary<string, string> bucket)
	{
		switch (local)
		{
			case "assignee":
				if (child.Attribute("value")?.Value is { } cav && cav.Length > 0)
				{
					bucket["cib:assignee"] = cav;
				}

				break;
			case "formField":
				NormalizeCibFormField(child, bucket);
				break;

			case "connector":
				NormalizeCibConnector(child, bucket);
				break;

			case "aiModule":
				var type = child.Attribute("type")?.Value;
				var model = child.Attribute("model")?.Value;
				var keyPrefix = !string.IsNullOrEmpty(type) ? $"cib:aiModule.{type}" : NextIndexedKey(bucket, "cib:aiModule");
				if (!string.IsNullOrEmpty(model))
				{
					bucket[$"{keyPrefix}.model"] = model;
				}

				break;
		}
	}

	private static void NormalizeJbpmExtension(XElement child, string local, Dictionary<string, string> bucket)
	{
		switch (local)
		{
			case "assignment":
				var actor = child.Attribute("actorId")?.Value;
				var grp = child.Attribute("groupId")?.Value;
				if (!string.IsNullOrEmpty(actor))
				{
					bucket["jbpm:assignment.actorId"] = actor;
				}

				if (!string.IsNullOrEmpty(grp))
				{
					bucket["jbpm:assignment.groupId"] = grp;
				}

				break;

			case "workItemHandler":
				var name = child.Attribute("name")?.Value;
				var cls = child.Attribute("class")?.Value;
				var keyPrefix = !string.IsNullOrEmpty(name) ? $"jbpm:workItemHandler.{name}" : NextIndexedKey(bucket, "jbpm:workItemHandler");
				if (!string.IsNullOrEmpty(cls))
				{
					bucket[$"{keyPrefix}.class"] = cls;
				}

				break;
		}
	}

	private static void NormalizeOsmanthusExtension(XElement child, string local, Dictionary<string, string> bucket)
	{
		switch (local)
		{
			case "advance":
				var type = child.Attribute("type")?.Value;
				var target = child.Attribute("target")?.Value;
				if (!string.IsNullOrEmpty(type))
				{
					bucket["osmanthus:advance.type"] = type;
				}

				if (!string.IsNullOrEmpty(target))
				{
					bucket["osmanthus:advance.target"] = target;
				}

				break;

			case "timeout":
				var dur = child.Attribute("duration")?.Value;
				var act = child.Attribute("action")?.Value;
				if (!string.IsNullOrEmpty(dur))
				{
					bucket["osmanthus:timeout.duration"] = dur;
				}

				if (!string.IsNullOrEmpty(act))
				{
					bucket["osmanthus:timeout.action"] = act;
				}

				break;

			case "pdfTemplate":
				var tid = child.Attribute("templateId")?.Value;
				var outp = child.Attribute("output")?.Value;
				if (!string.IsNullOrEmpty(tid))
				{
					bucket["osmanthus:pdfTemplate.templateId"] = tid;
				}

				if (!string.IsNullOrEmpty(outp))
				{
					bucket["osmanthus:pdfTemplate.output"] = outp;
				}

				break;
		}
	}

	private static void NormalizeAlfrescoExtension(XElement child, string local, Dictionary<string, string> bucket)
	{
		switch (local)
		{
			case "formKey":
				if (child.Attribute("value")?.Value is { } fk && fk.Length > 0)
				{
					bucket["alfresco:formKey"] = fk;
				}

				break;
			case "scriptTask":
				if (child.Attribute("script")?.Value is { } sc && sc.Length > 0)
				{
					bucket["alfresco:scriptTask.script"] = sc;
				}

				break;
		}
	}

}
