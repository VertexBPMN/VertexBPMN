using System.Text.Json.Serialization;

namespace VertexBPMN.Studio.Services;

internal interface IConnectorTemplateService
{
    Task<IReadOnlyList<StudioConnectorTemplate>> ListAsync(string tenantId, CancellationToken cancellationToken = default);
}
