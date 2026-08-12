using Microsoft.EntityFrameworkCore;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Domain.Model.Dmn;

namespace VertexBPMN.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IDecisionRepository over DecisionDbContext.
/// DecisionTable is transient (not persisted) and parsed from DmnXml on demand.
/// </summary>
public class DecisionRepository : IDecisionRepository
{
    private readonly DecisionDbContext _db;
    public DecisionRepository(DecisionDbContext db) => _db = db;

    public async ValueTask UpsertDefinitionAsync(DecisionDefinition definition, CancellationToken ct = default)
    {
        var id = DecisionDefinition.BuildId(definition.Key, definition.TenantId);
        var existing = await _db.DecisionDefinitions
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (existing == null)
        {
            if (definition.DecisionTable == null && !string.IsNullOrWhiteSpace(definition.DmnXml))
            {
                // Parse for in-memory use; not persisted
                definition.DecisionTable = DmnDecisionTable.Parse(definition.DmnXml);
            }
            await _db.DecisionDefinitions.AddAsync(definition, ct);
        }
        else
        {
            bool dmnChanged = !string.Equals(existing.DmnXml, definition.DmnXml, StringComparison.Ordinal);

            existing.Name = definition.Name;
            existing.DmnXml = definition.DmnXml;

            if (definition.DecisionTable != null)
            {
                existing.DecisionTable = definition.DecisionTable; // transient
            }
            else if (dmnChanged && !string.IsNullOrWhiteSpace(definition.DmnXml))
            {
                existing.DecisionTable = DmnDecisionTable.Parse(definition.DmnXml);
            }
        }
    }

    public async ValueTask<DecisionDefinition?> GetDefinitionAsync(string key, string? tenantId = null, CancellationToken ct = default)
    {
        var id = DecisionDefinition.BuildId(key, tenantId);
        var entity = await _db.DecisionDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (entity != null)
            EnsureParsed(entity);

        return entity;
    }

    public async IAsyncEnumerable<DecisionDefinition> ListDefinitionsAsync(string? key = null, string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var query = _db.DecisionDefinitions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(key))
            query = query.Where(d => d.Key == key);
        if (!string.IsNullOrWhiteSpace(tenantId))
            query = query.Where(d => d.TenantId == tenantId);

        await foreach (var d in query
            .OrderBy(d => d.Key)
            .AsAsyncEnumerable()
            .WithCancellation(ct))
        {
            EnsureParsed(d);
            yield return d;
        }
    }

    public async ValueTask AddInstanceAsync(DecisionInstance instance, CancellationToken ct = default)
        => await _db.DecisionInstances.AddAsync(instance, ct);

    public async IAsyncEnumerable<DecisionInstance> ListInstancesAsync(string? decisionKey = null, string? tenantId = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var query = _db.DecisionInstances.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(decisionKey))
            query = query.Where(i => i.DecisionDefinitionKey == decisionKey);
        if (!string.IsNullOrWhiteSpace(tenantId))
            query = query.Where(i => i.TenantId == tenantId);

        await foreach (var i in query
            .OrderByDescending(i => i.EvaluationTime)
            .AsAsyncEnumerable()
            .WithCancellation(ct))
        {
            yield return i;
        }
    }

    public async ValueTask SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);

    private static void EnsureParsed(DecisionDefinition def)
    {
        if (def.DecisionTable == null && !string.IsNullOrWhiteSpace(def.DmnXml))
        {
            def.DecisionTable = DmnDecisionTable.Parse(def.DmnXml);
        }
    }
}
