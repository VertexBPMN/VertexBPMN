using VertexBPMN.Domain;

namespace VertexBPMN.Core.Contracts;

public interface IIncidentService
{
    IAsyncEnumerable<Incident> ListAsync();
    Task<Incident?> GetByIdAsync(Guid id);
    // Weitere Methoden: Create, Resolve, etc. bei Bedarf
}
