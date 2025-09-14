using VertexBPMN.Domain.Entities;

namespace VertexBPMN.Domain.Interfaces;

public interface IIncidentService
{
    IAsyncEnumerable<Incident> ListAsync();
    Task<Incident?> GetByIdAsync(Guid id);
    // Weitere Methoden: Create, Resolve, etc. bei Bedarf
}
