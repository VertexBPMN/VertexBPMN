using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VertexBPMN.Domain.Contracts;

public interface IIncidentService
{
    IAsyncEnumerable<Incident> ListAsync();
    Task<Incident?> GetByIdAsync(Guid id);
    // Weitere Methoden: Create, Resolve, etc. bei Bedarf
}
