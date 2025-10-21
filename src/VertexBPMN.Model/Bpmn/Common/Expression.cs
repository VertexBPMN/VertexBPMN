using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Common;

#nullable enable

/// <summary>
/// Abstract expression class, as per Figure 8.21.
/// </summary>
public abstract record Expression(string Body = "") : BaseElement();