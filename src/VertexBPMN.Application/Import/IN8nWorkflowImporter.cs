using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Import;

public interface IN8nWorkflowImporter
{
	N8nImportResult Import(string workflowJson);
	N8nImportResult Import(string workflowJson, IReadOnlyList<CredentialMetadata> credentials);
}
