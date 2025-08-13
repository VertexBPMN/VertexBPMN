using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VertexBPMN.Core.Domain;

namespace VertexBPMN.Core.Services;

public interface IIncidentService
{
    IAsyncEnumerable<Incident> ListAsync();
    Task<Incident?> GetByIdAsync(Guid id);
    // Weitere Methoden: Create, Resolve, etc. bei Bedarf
}
