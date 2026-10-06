using System.Collections.ObjectModel;

namespace VertexBPMN.Domain.Model.Validation;

public sealed record ValidationRuleDescriptor(
	string Code,
	string Category,
	ValidationSeverity DefaultSeverity,
	string Title,
	string Description);
