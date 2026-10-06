using System.Text.Json;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Import;

public sealed record OpenApiImportReportItem(string OperationId, N8nImportDisposition Disposition, string Message);
