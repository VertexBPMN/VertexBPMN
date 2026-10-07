using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Import;

public sealed record N8nImportReportItem(string NodeName, string NodeType, N8nImportDisposition Disposition, string Message);
