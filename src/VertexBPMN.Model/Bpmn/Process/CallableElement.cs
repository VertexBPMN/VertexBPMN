using System.Collections.Generic;
using VertexBPMN.Domain.Model.Bpmn.Common;
using VertexBPMN.Domain.Model.Bpmn.Foundation;

namespace VertexBPMN.Domain.Model.Bpmn.Process;

#nullable enable

/// <summary>
/// Callable element, as per Figure 10.43.
/// </summary>
public abstract record CallableElement(
    string? Name = null,
    InputOutputSpecification? IoSpecification = null,
    List<InputOutputBinding> IoBindings = null!,
    List<ResourceRole> SupportedInterfaceRefs = null!
) : RootElement();