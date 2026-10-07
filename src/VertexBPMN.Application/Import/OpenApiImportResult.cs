using System.Text.Json;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Import;

public sealed record OpenApiImportResult(
	IReadOnlyList<ConnectorTemplateWriteRequest> Templates,
	IReadOnlyList<OpenApiImportReportItem> Report);
