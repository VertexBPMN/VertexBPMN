namespace VertexBPMN.Sdk;
public sealed record N8nImportResult(string BpmnXml, IReadOnlyList<N8nImportReportItem> Report);
