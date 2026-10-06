using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Import;

public sealed record N8nImportResult(string BpmnXml, IReadOnlyList<N8nImportReportItem> Report);
