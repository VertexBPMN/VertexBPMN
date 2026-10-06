using System.Text.Json;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Import;

public interface IOpenApiConnectorTemplateImporter
{
	OpenApiImportResult Import(string openApiJsonOrYaml, string tenantId);
}
