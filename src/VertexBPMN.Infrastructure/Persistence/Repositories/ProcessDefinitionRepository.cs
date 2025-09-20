using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces.Repositories;

namespace VertexBPMN.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IProcessDefinitionRepository.
/// </summary>
public class ProcessDefinitionRepository : IProcessDefinitionRepository
{
    private readonly BpmnDbContext _db;
    public ProcessDefinitionRepository(BpmnDbContext db) => _db = db;

    public async ValueTask AddAsync(ProcessDefinition definition, CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.ProcessDefinitions.AddAsync(definition, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Hier die Innere Ausnahme abfangen und loggen
            var innerExceptionMessage = ex.InnerException?.Message;

            // Logge hier die genaue Fehlermeldung und die Objektdaten
            Console.WriteLine($"DbUpdateException aufgetreten: {ex.Message}");
            Console.WriteLine($"Innere Ausnahme: {innerExceptionMessage}");
            Console.WriteLine($"Fehlerhafte Daten: Id={definition.Id}, Key={definition.Key}, TenantId={definition.TenantId}");

            // Wirf die Ausnahme erneut, damit der Aufrufer den Fehler behandeln kann
            throw;
        }
    }

    public async ValueTask<ProcessDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.ProcessDefinitions.FindAsync(new object[] { id }, cancellationToken);
    }

    public async ValueTask<ProcessDefinition?> GetLatestByKeyAsync(string key, string? tenantId = null, CancellationToken cancellationToken = default)
    {
        return await _db.ProcessDefinitions
            .Where(d => d.Key == key && (tenantId == null || d.TenantId == tenantId))
            .OrderByDescending(d => d.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async IAsyncEnumerable<ProcessDefinition> ListAsync(string? key = null, string? tenantId = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var query = _db.ProcessDefinitions.AsQueryable();
        if (key != null) query = query.Where(d => d.Key == key);
        if (tenantId != null) query = query.Where(d => d.TenantId == tenantId);
        await foreach (var def in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            yield return def;
    }

    public async ValueTask DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.ProcessDefinitions.FindAsync(new object[] { id }, cancellationToken);
        if (entity != null)
        {
            _db.ProcessDefinitions.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
